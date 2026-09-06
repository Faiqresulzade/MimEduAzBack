using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Services;

/// <summary>Cari sorğunun JWT claim-lərindən istifadəçi məlumatını oxuyur.</summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsAdmin => Principal?.IsInRole(AppRoles.Admin) == true;

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedException();
}
