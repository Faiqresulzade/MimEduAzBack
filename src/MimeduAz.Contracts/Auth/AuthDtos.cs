using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Auth;

/// <summary>
/// Qeydiyyat. <paramref name="AccountType"/> göndərilməsə <see cref="Domain.Enums.AccountType.Student"/>
/// qəbul edilir — yəni default hesab təlim alan istifadəçidir.
/// </summary>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string? Subject,
    AccountType? AccountType = null,
    /// <summary>İstifadə şərtləri və məxfilik siyasəti ilə razılıq - qeydiyyat üçün məcburidir.</summary>
    bool AcceptTerms = false);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

/// <summary>Şagird hesabını müəllif (Teacher) hesabına yüksəltmək üçün.</summary>
public sealed record BecomeAuthorRequest(string? Subject);

/// <summary>Giriş/qeydiyyat cavabı.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string? Subject,
    IReadOnlyList<string> Roles,
    /// <summary>Resurs yükləyib sata bilirmi (Teacher və ya Admin).</summary>
    bool CanPublishResources,
    DateTime CreatedAt);

/// <summary>
/// Google ilə giriş. <paramref name="IdToken"/> — Google Identity Services-in verdiyi "credential".
/// Yeni istifadəçi üçün ilk sorğu <paramref name="AccountType"/>-siz gəlir; cavabda
/// <see cref="GoogleLoginResponse.RequiresAccountType"/> true olarsa eyni token hesab növü ilə yenidən göndərilir.
/// </summary>
public sealed record GoogleLoginRequest(string IdToken, AccountType? AccountType = null, bool AcceptTerms = false);

/// <summary>
/// Hesab varsa <see cref="Auth"/> doludur. Yoxdursa hesab hələ yaradılmır —
/// <see cref="RequiresAccountType"/> true, frontend "Müəlliməm / Şagirdəm" soruşur.
/// </summary>
public sealed record GoogleLoginResponse(
    bool RequiresAccountType,
    AuthResponse? Auth,
    string? Email,
    string? FullName);

/// <summary>Frontend Google düyməsini yalnız ClientId doludursa göstərir.</summary>
public sealed record GoogleConfigResponse(string? ClientId);
