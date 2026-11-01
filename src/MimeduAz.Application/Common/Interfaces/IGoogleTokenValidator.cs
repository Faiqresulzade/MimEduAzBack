namespace MimeduAz.Application.Common.Interfaces;

/// <summary>Google-un təsdiqlədiyi istifadəçi (ID token-dən).</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>
/// Google ID token-in imzasını, vaxtını və auditoriyasını (bizim Client ID) yoxlayır.
/// Token etibarsızdırsa null qaytarır.
/// </summary>
public interface IGoogleTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct);
}
