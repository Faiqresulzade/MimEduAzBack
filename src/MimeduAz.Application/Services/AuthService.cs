using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Auth;
using MimeduAz.Domain.Constants;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenGenerator _tokens;
    private readonly ICurrentUserService _currentUser;
    private readonly JwtOptions _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext db,
        IJwtTokenGenerator tokens,
        ICurrentUserService currentUser,
        IOptions<JwtOptions> jwt,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _currentUser = currentUser;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("Bu e-poçt ünvanı ilə artıq hesab mövcuddur.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new ValidationFailedException(ToErrorDictionary(created));
        }

        // admin@mimedu.az avtomatik Admin rolu alır (prototipdəki davranış).
        var role = IsAdminEmail(email) ? AppRoles.Admin : AppRoles.Teacher;
        await _userManager.AddToRoleAsync(user, role);

        _logger.LogInformation("Yeni istifadəçi qeydiyyatdan keçdi. UserId: {UserId}, Rol: {Role}", user.Id, role);

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Hansının səhv olduğunu bildirmirik - hesabların sadalanmasının qarşısını alır.
            throw new UnauthorizedException("E-poçt və ya şifrə yanlışdır.");
        }

        // admin@mimedu.az əvvəlcədən yaradılıbsa belə Admin rolunun olmasını təmin edirik.
        if (IsAdminEmail(user.Email) && !await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            await _userManager.AddToRoleAsync(user, AppRoles.Admin);
        }

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (stored is null || !stored.IsActive)
        {
            throw new UnauthorizedException("Refresh token etibarsızdır və ya vaxtı bitib.");
        }

        var user = stored.User ?? throw new UnauthorizedException("Refresh token etibarsızdır.");

        // Rotasiya: köhnə token ləğv olunur, yerinə yenisi verilir.
        var newTokenValue = _tokens.CreateRefreshTokenValue();
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByToken = newTokenValue;

        var replacement = new RefreshToken
        {
            UserId = user.Id,
            Token = newTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays)
        };
        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);

        var roles = await _userManager.GetRolesAsync(user);
        var access = _tokens.CreateAccessToken(user, roles);

        return new AuthResponse(access.Value, newTokenValue, access.ExpiresAt, ToDto(user, roles));
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (stored is null || !stored.IsActive)
        {
            // Artıq etibarsızdırsa nəticə eynidir - idempotent davranış.
            return;
        }

        stored.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var roles = await _userManager.GetRolesAsync(user);
        return ToDto(user, roles);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var access = _tokens.CreateAccessToken(user, roles);

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _tokens.CreateRefreshTokenValue(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays)
        };

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(access.Value, refresh.Token, access.ExpiresAt, ToDto(user, roles));
    }

    private static bool IsAdminEmail(string? email) =>
        string.Equals(email?.Trim(), AppDefaults.AdminEmail, StringComparison.OrdinalIgnoreCase);

    private static UserDto ToDto(ApplicationUser user, IList<string> roles) => new(
        user.Id,
        user.FullName,
        user.Email ?? string.Empty,
        user.Subject,
        roles.ToList(),
        user.CreatedAt);

    private static Dictionary<string, string[]> ToErrorDictionary(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code.Contains("Password", StringComparison.OrdinalIgnoreCase) ? "password" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
