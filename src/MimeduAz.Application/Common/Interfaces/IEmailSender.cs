namespace MimeduAz.Application.Common.Interfaces;

/// <summary>Göndəriləcək məktub.</summary>
public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string HtmlBody);

/// <summary>SMTP üzərindən məktub göndərir.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>
/// Məktubu yaddaşdakı növbəyə atır. Göndərmə arxa planda baş verir ki,
/// SMTP yavaş və ya əlçatmaz olanda istifadəçinin sorğusu ləngiməsin.
/// </summary>
public interface IEmailQueue
{
    bool TryEnqueue(EmailMessage message);
}
