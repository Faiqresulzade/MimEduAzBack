using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>
/// Müəllimin yaratdığı sınaq imtahanı. Resurslar kimi moderasiyadan keçir,
/// təlimlər kimi səbətdən satın alınır.
/// </summary>
public class Exam
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Ümumi etiket, məs. "Riyaziyyat" və ya "Buraxılış sınağı". Filtrləmə üçün.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Sinif (1-11). Sinifdən asılı olmayan sınaqlarda null.</summary>
    public int? Grade { get; set; }

    public Guid AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    /// <summary>
    /// İştirakçının sınağı bitirmək üçün dəqiqə ilə vaxtı. Sayğac sınaq
    /// başladılan andan işləyir - təqvim tarixi yoxdur.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>Sertifikat almaq üçün lazım olan minimum faiz.</summary>
    public int PassPercent { get; set; } = 60;

    public bool IsPaid { get; set; }

    /// <summary>AZN ilə qiymət. Pulsuz sınaqlarda 0.</summary>
    public decimal Price { get; set; }

    public ExamStatus Status { get; set; } = ExamStatus.Pending;

    /// <summary>Moderasiya rədd edilibsə səbəb.</summary>
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    public ICollection<ExamSection> Sections { get; set; } = new List<ExamSection>();
    public ICollection<ExamAttempt> Attempts { get; set; } = new List<ExamAttempt>();
}
