using Microsoft.AspNetCore.Identity;

namespace MimeduAz.Domain.Entities;

/// <summary>Identity rolu (Teacher / Admin).</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}
