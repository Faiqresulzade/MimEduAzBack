namespace MimeduAz.Domain.Entities;

/// <summary>
/// Təlimin bir dərsi — video və izah. Məzmun yalnız təlimə yazılmış
/// istifadəçilərə (və adminə) verilir.
/// </summary>
public class TrainingLesson
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TrainingId { get; set; }
    public Training? Training { get; set; }

    public int OrderIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Video linki (YouTube/Vimeo). Yalnız yazılmış istifadəçiyə göndərilir.</summary>
    public string? VideoUrl { get; set; }

    /// <summary>Dərsin təxmini müddəti (dəqiqə). UI-də göstərilir.</summary>
    public int? DurationMinutes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LessonCompletion> Completions { get; set; } = new List<LessonCompletion>();
}
