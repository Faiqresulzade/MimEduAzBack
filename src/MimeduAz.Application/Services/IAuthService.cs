using MimeduAz.Contracts.Auth;

namespace MimeduAz.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct);
    Task<UserDto> GetCurrentUserAsync(CancellationToken ct);
}
