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
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenGenerator _tokens;
    private const string GoogleProvider = "Google";

    private readonly ICurrentUserService _currentUser;
    private readonly IGoogleTokenValidator _google;
    private readonly JwtOptions _jwt;
    private readonly GoogleAuthOptions _googleOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext db,
        IJwtTokenGenerator tokens,
        ICurrentUserService currentUser,
        IGoogleTokenValidator google,
        IOptions<JwtOptions> jwt,
        IOptions<GoogleAuthOptions> googleOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _currentUser = currentUser;
        _google = google;
        _jwt = jwt.Value;
        _googleOptions = googleOptions.Value;
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
        // Qalan hallarda hesab növü seçilir; göndərilməsə şagird sayılır -
        // yəni default istifadəçi təlim alan, material satmayan hesabdır.
        var role = IsAdminEmail(email)
            ? AppRoles.Admin
            : request.AccountType == AccountType.Teacher
                ? AppRoles.Teacher
                : AppRoles.Student;
        await _userManager.AddToRoleAsync(user, role);

        _logger.LogInformation("Yeni istifadəçi qeydiyyatdan keçdi. UserId: {UserId}, Rol: {Role}", user.Id, role);

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());

        // Google ilə açılmış hesabın şifrəsi yoxdur. Qeydiyyat onsuz da e-poçtun mövcudluğunu
        // bildirdiyi üçün bu mesaj əlavə məlumat sızdırmır, amma istifadəçini düz yola yönəldir.
        if (user is not null && !await _userManager.HasPasswordAsync(user))
        {
            throw new UnauthorizedException("Bu hesab Google ilə açılıb — \"Google ilə daxil ol\" düyməsindən istifadə edin.");
        }

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

    public GoogleConfigResponse GetGoogleConfig() =>
        new(_googleOptions.IsConfigured ? _googleOptions.ClientId : null);

    public async Task<GoogleLoginResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken ct)
    {
        if (!_googleOptions.IsConfigured)
        {
            throw ExternalServiceException.Unavailable("Google ilə giriş hələ qurulmayıb.");
        }

        var identity = await _google.ValidateAsync(request.IdToken, ct)
                       ?? throw new UnauthorizedException("Google girişi təsdiqlənmədi. Yenidən cəhd edin.");

        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            throw new UnauthorizedException("Google hesabınızın e-poçtu təsdiqlənməyib.");
        }

        // Əvvəl Google bağlantısı ilə, sonra e-poçtla axtarılır: Google e-poçtu təsdiqlədiyi
        // üçün şifrə ilə açılmış köhnə hesab avtomatik birləşir.
        var user = await _userManager.FindByLoginAsync(GoogleProvider, identity.Subject)
                   ?? await _userManager.FindByEmailAsync(identity.Email);

        if (user is not null)
        {
            await LinkGoogleAsync(user, identity);

            if (IsAdminEmail(user.Email) && !await _userManager.IsInRoleAsync(user, AppRoles.Admin))
            {
                await _userManager.AddToRoleAsync(user, AppRoles.Admin);
            }

            return new GoogleLoginResponse(false, await BuildAuthResponseAsync(user, ct), user.Email, user.FullName);
        }

        // Yeni istifadəçi: hesab növü seçilməyibsə hələ heç nə yaradılmır.
        var fullName = string.IsNullOrWhiteSpace(identity.Name)
            ? identity.Email.Split('@')[0]
            : identity.Name.Trim();

        if (request.AccountType is null && !IsAdminEmail(identity.Email))
        {
            return new GoogleLoginResponse(true, null, identity.Email, fullName);
        }

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = identity.Email,
            Email = identity.Email,
            EmailConfirmed = true,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow
        };

        // Şifrəsiz yaradılır - giriş yalnız Google ilə.
        var created = await _userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            throw new ValidationFailedException(ToErrorDictionary(created));
        }

        var role = IsAdminEmail(identity.Email)
            ? AppRoles.Admin
            : request.AccountType == AccountType.Teacher ? AppRoles.Teacher : AppRoles.Student;
        await _userManager.AddToRoleAsync(user, role);
        await LinkGoogleAsync(user, identity);

        _logger.LogInformation("Google ilə yeni istifadəçi. UserId: {UserId}, Rol: {Role}", user.Id, role);

        return new GoogleLoginResponse(false, await BuildAuthResponseAsync(user, ct), user.Email, user.FullName);
    }

    /// <summary>Google hesabını istifadəçiyə bağlayır (user_logins) və e-poçtu təsdiqlənmiş sayır.</summary>
    private async Task LinkGoogleAsync(ApplicationUser user, GoogleIdentity identity)
    {
        var logins = await _userManager.GetLoginsAsync(user);
        if (!logins.Any(l => l.LoginProvider == GoogleProvider))
        {
            await _userManager.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, identity.Subject, GoogleProvider));
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }
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

    public async Task<UserDto> BecomeAuthorAsync(BecomeAuthorRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        if (!string.IsNullOrWhiteSpace(request.Subject))
        {
            user.Subject = request.Subject.Trim();
            await _db.SaveChangesAsync(ct);
        }

        if (!await _userManager.IsInRoleAsync(user, AppRoles.Teacher))
        {
            var added = await _userManager.AddToRoleAsync(user, AppRoles.Teacher);
            if (!added.Succeeded)
            {
                throw new BadRequestException("Müəllif rolu təyin edilə bilmədi.");
            }

            _logger.LogInformation("İstifadəçi müəllif oldu. UserId: {UserId}", user.Id);
        }

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
        roles.Any(r => r is AppRoles.Teacher or AppRoles.Admin),
        user.CreatedAt);

    private static Dictionary<string, string[]> ToErrorDictionary(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code.Contains("Password", StringComparison.OrdinalIgnoreCase) ? "password" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
