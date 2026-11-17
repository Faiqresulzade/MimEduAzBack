using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Services;

namespace MimeduAz.Infrastructure.Payments;

/// <summary>
/// İstifadəçi bank səhifəsini bağlayıbsa callback gəlməyə bilər. Bu iş Pending sifarişləri
/// müəyyən intervalla bankdan (TRTYPE=90) soruşur, alınmamış tamamlamaları təkrarlayır
/// və 24 saatdan köhnə ödənilməmiş sifarişləri ləğv edir.
/// </summary>
public sealed class PaymentStatusWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PaymentOptions _options;
    private readonly ILogger<PaymentStatusWorker> _logger;

    public PaymentStatusWorker(
        IServiceScopeFactory scopes, IOptions<PaymentOptions> options, ILogger<PaymentStatusWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.StatusCheckIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_options.Enabled)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPaymentService>().ProcessPendingAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Ödəniş status yoxlaması uğursuz oldu.");
                }
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
