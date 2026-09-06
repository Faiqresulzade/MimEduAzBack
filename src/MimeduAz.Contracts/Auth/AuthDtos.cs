namespace MimeduAz.Contracts.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password, string? Subject);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

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
    DateTime CreatedAt);
