namespace MimeduAz.Domain.Entities;

/// <summary>Rotasiya olunan refresh token. Access token bitəndə yenisini almaq üçün istifadə olunur.</summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    /// <summary>Rotasiya zamanı bu token-in yerinə verilən yeni token.</summary>
    public string? ReplacedByToken { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
