using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Entities;
using MimeduAz.Infrastructure.Persistence;

namespace MimeduAz.Infrastructure.Logging;

/// <summary>
/// Növbədəki audit qeydlərini batch şəklində bazaya yazır və köhnə qeydləri təmizləyir.
/// Yazma xətası heç vaxt tətbiqi dayandırmır - yalnız loglanır.
/// </summary>
public sealed class RequestLogWriter : BackgroundService
{
    private readonly RequestLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RequestLogOptions _options;
    private readonly ILogger<RequestLogWriter> _logger;

    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public RequestLogWriter(
        RequestLogQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<RequestLogOptions> options,
        ILogger<RequestLogWriter> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var flushInterval = TimeSpan.FromSeconds(Math.Max(1, _options.FlushIntervalSeconds));
        var batch = new List<RequestLog>(_options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(batch, flushInterval, stoppingToken);

                if (batch.Count > 0)
                {
                    await PersistAsync(batch, stoppingToken);
                    batch.Clear();
                }

                await CleanupIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log yazılarkən xəta baş verdi.");
                batch.Clear();

                // Baza müvəqqəti əlçatmazdırsa dayanmadan təkrar cəhd etməyək.
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        // Dayanarkən növbədə qalanları yazmağa çalışırıq.
        await FlushRemainingAsync();
    }

    /// <summary>Batch dolana və ya flush intervalı bitənə qədər növbədən qeyd toplayır.</summary>
    private async Task DrainAsync(List<RequestLog> batch, TimeSpan flushInterval, CancellationToken ct)
    {
        // İlk qeydi gözləyirik - boşdursa CPU-nu yandırmırıq.
        if (!await _queue.Reader.WaitToReadAsync(ct))
        {
            return;
        }

        using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
        window.CancelAfter(flushInterval);

        try
        {
            while (batch.Count < _options.BatchSize
                   && await _queue.Reader.WaitToReadAsync(window.Token))
            {
                while (batch.Count < _options.BatchSize && _queue.Reader.TryRead(out var log))
                {
                    batch.Add(log);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Flush intervalı bitdi - əlimizdəkiləri yazırıq.
        }
    }

    private async Task PersistAsync(List<RequestLog> batch, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.RequestLogs.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }

    private async Task CleanupIfDueAsync(CancellationToken ct)
    {
        if (_options.RetentionDays <= 0 || DateTime.UtcNow - _lastCleanupUtc < TimeSpan.FromHours(24))
        {
            return;
        }

        _lastCleanupUtc = DateTime.UtcNow;
        var threshold = DateTime.UtcNow.AddDays(-_options.RetentionDays);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deleted = await db.RequestLogs
            .Where(l => l.CreatedAt < threshold)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "{Count} köhnə audit qeydi silindi ({Days} gündən köhnə).", deleted, _options.RetentionDays);
        }
    }

    private async Task FlushRemainingAsync()
    {
        var remaining = new List<RequestLog>();
        while (_queue.Reader.TryRead(out var log))
        {
            remaining.Add(log);
        }

        if (remaining.Count == 0)
        {
            return;
        }

        try
        {
            await PersistAsync(remaining, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dayanma zamanı qalan audit qeydləri yazıla bilmədi.");
        }
    }
}
