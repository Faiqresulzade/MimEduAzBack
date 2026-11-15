using MimeduAz.Contracts.Auth;

namespace MimeduAz.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Frontend üçün Google Client ID (qurulmayıbsa null).</summary>
    GoogleConfigResponse GetGoogleConfig();

    /// <summary>
    /// Google ID token ilə giriş/qeydiyyat. Eyni e-poçtlu hesab varsa ona daxil olur.
    /// Yeni istifadəçidə hesab növü göndərilməyibsə <c>RequiresAccountType</c> qaytarır.
    /// </summary>
    Task<GoogleLoginResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct);
    Task<UserDto> GetCurrentUserAsync(CancellationToken ct);

    /// <summary>
    /// Şagird hesabını müəllif (Teacher) hesabına yüksəldir ki, Resurs Bankına
    /// material yükləyə bilsin. Artıq müəllifdirsə heç nə dəyişmir (idempotent).
    /// </summary>
    Task<UserDto> BecomeAuthorAsync(BecomeAuthorRequest request, CancellationToken ct);

    /// <summary>
    /// Cari istifadəçinin hesabını silir (Google Play tələbi). Şəxsi data (ad, e-poçt, fənn,
    /// şifrə, Google bağlantısı, səbət, token-lər) silinir; sifariş və sertifikat qeydləri
    /// başqa istifadəçilərin hüquqlarını qorumaq üçün adsız saxlanır. Geri qaytarılmır.
    /// </summary>
    Task DeleteCurrentAccountAsync(CancellationToken ct);
}
