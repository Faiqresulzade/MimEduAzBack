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
    AccountType? AccountType = null);

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
