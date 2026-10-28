namespace MimeduAz.Domain.Entities;

/// <summary>
/// KSQ (kiçik summativ qiymətləndirmə) cavab kartı yoxlaması üçün müəllimin açarı.
/// Bir test bir neçə sinifdə istifadə oluna bilər; A və B variantının açarı ayrıdır.
/// </summary>
public class KsqTest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Hər qapalı sualın balı (sıra ilə). Uzunluq = qapalı sual sayı.</summary>
    public List<int> ClosedPoints { get; set; } = new();

    /// <summary>Hər açıq sualın maksimum balı. Uzunluq = açıq sual sayı.</summary>
    public List<int> OpenPoints { get; set; } = new();

    /// <summary>A variantının düzgün cavabları ("A".."E", seçilməyibsə null).</summary>
    public List<string?> KeyA { get; set; } = new();

    /// <summary>B variantının düzgün cavabları.</summary>
    public List<string?> KeyB { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<KsqResult> Results { get; set; } = new List<KsqResult>();
}
