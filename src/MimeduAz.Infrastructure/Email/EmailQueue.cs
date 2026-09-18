using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeduAz.Application.Common.Interfaces;

namespace MimeduAz.Infrastructure.Email;

/// <summary>
/// Məktublar üçün yaddaşdakı məhdud tutumlu növbə. SMTP yavaş və ya əlçatmaz
/// olanda istifadəçinin sorğusu gözləməsin deyə göndərmə arxa plana keçirilir.
/// </summary>
public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public bool TryEnqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
}

/// <summary>Növbədəki məktubları bir-bir göndərir. Xəta heç vaxt tətbiqi dayandırmır.</summary>
public sealed class EmailBackgroundSender : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailBackgroundSender> _logger;

    public EmailBackgroundSender(
        EmailQueue queue,
        IEmailSender sender,
        ILogger<EmailBackgroundSender> logger)
    {
        _queue = queue;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _sender.SendAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Məktub göndərilə bilmədi. Mövzu: {Subject}", message.Subject);
            }
        }
    }
}
