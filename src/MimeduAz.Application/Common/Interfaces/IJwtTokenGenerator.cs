using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Interfaces;

public sealed record AccessToken(string Value, DateTime ExpiresAt);

public interface IJwtTokenGenerator
{
    AccessToken CreateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>Kriptoqrafik təsadüfi refresh token dəyəri yaradır.</summary>
    string CreateRefreshTokenValue();
}
