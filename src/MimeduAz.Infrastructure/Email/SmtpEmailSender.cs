using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;

namespace MimeduAz.Infrastructure.Email;

/// <summary>
/// MailKit üzərindən SMTP göndərişi. SMTP konfiqurasiya olunmayıbsa məktub
/// göndərilmir - yalnız loga yazılır, xəta atılmır.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            // Konfiqurasiya yoxdursa bu, xəta deyil - sistem SMTP-siz də işləməlidir.
            _logger.LogInformation(
                "SMTP konfiqurasiya olunmayıb, məktub göndərilmədi. Mövzu: {Subject}, Alıcı sayı: {Count}",
                message.Subject, message.To.Count);
            return;
        }

        // IsConfigured host və from ünvanını artıq yoxlayır - burada null ola bilməz.
        var host = _options.Host!;
        var fromAddress = _options.FromAddress!;

        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(_options.FromName, fromAddress));

        foreach (var to in message.To)
        {
            mail.To.Add(MailboxAddress.Parse(to));
        }

        mail.Subject = message.Subject;
        mail.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = Math.Max(5, _options.TimeoutSeconds) * 1000
        };

        var socketOptions = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(host, _options.Port, socketOptions, ct);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct);
        }

        await client.SendAsync(mail, ct);
        await client.DisconnectAsync(quit: true, ct);

        _logger.LogInformation(
            "Məktub göndərildi. Mövzu: {Subject}, Alıcı sayı: {Count}",
            message.Subject, message.To.Count);
    }
}
