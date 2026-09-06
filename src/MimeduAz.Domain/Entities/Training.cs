using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>Müəllimlər üçün təlim.</summary>
public class Training
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public TrainingFormat Format { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationHours { get; set; }

    /// <summary>UI-də göstərilən qısa etiket, məs. "2 gün · 8 saat".</summary>
    public string MetaLabel { get; set; } = string.Empty;

    /// <summary>Yer limiti. Yalnız Live formatda məhdud olur, digərlərində null (limitsiz).</summary>
    public int? SeatLimit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TrainingSyllabusItem> SyllabusItems { get; set; } = new List<TrainingSyllabusItem>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
