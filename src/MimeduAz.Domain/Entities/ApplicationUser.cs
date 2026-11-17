using Microsoft.AspNetCore.Identity;

namespace MimeduAz.Domain.Entities;

/// <summary>Platformanın istifadəçisi (müəllim və ya admin).</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Müəllimin əsas fənni, məs. "Riyaziyyat". Admin üçün boş ola bilər.</summary>
    public string? Subject { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// İstifadə şərtləri və məxfilik siyasəti ilə razılaşdığı an və o anki versiya.
    /// Bu tətbiqdən əvvəl açılmış hesablarda null.
    /// </summary>
    public DateTime? TermsAcceptedAt { get; set; }
    public string? TermsVersion { get; set; }

    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
