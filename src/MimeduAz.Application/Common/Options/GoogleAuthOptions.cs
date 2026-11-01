namespace MimeduAz.Application.Common.Options;

/// <summary>
/// Google ilə giriş. appsettings.json -> "GoogleAuth", production-da env: <c>GoogleAuth__ClientId</c>.
/// ClientId boşdursa Google girişi söndürülüb sayılır və düymə göstərilmir.
/// Client Secret lazım deyil — yalnız ID token yoxlanılır.
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public string? ClientId { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
