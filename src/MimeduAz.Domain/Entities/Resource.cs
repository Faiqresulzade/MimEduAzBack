using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>Resurs Bankına yüklənən dərs materialı.</summary>
public class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Fənn, məs. "Riyaziyyat".</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Sinif (1-11).</summary>
    public int Grade { get; set; }

    public ResourceType Type { get; set; }

    public Guid AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    public int Downloads { get; set; }

    public bool IsPaid { get; set; }

    /// <summary>AZN ilə qiymət. Pulsuz resurslarda 0.</summary>
    public decimal Price { get; set; }

    public ResourceStatus Status { get; set; } = ResourceStatus.Pending;

    /// <summary>Fayl saxlayıcısındakı nisbi yol. <see cref="Status"/> Pending olsa belə doldurulur.</summary>
    public string? FilePath { get; set; }

    /// <summary>İstifadəçinin yüklədiyi orijinal fayl adı (endirmə zamanı qaytarılır).</summary>
    public string? OriginalFileName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Moderasiya rədd edilibsə səbəb.</summary>
    public string? RejectionReason { get; set; }

    public Quiz? Quiz { get; set; }
}
