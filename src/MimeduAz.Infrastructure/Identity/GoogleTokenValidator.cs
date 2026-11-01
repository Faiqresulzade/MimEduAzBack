using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;

namespace MimeduAz.Infrastructure.Identity;

/// <summary>
/// Google ID token-i Google-un açıq açarları ilə yoxlayır (imza, vaxt, issuer və
/// auditoriya = bizim Client ID). Açarlar kitabxana tərəfindən keşlənir.
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IOptions<GoogleAuthOptions> options, ILogger<GoogleTokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _options.ClientId! } });

            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            // Token-in özü loglanmır - yalnız səbəb.
            _logger.LogWarning("Google ID token etibarsızdır: {Reason}", ex.Message);
            return null;
        }
    }
}
