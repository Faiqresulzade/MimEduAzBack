namespace MimeduAz.Domain.Entities;

/// <summary>
/// Cəhdin bir sual üzrə cavabı. Saxlanılır ki, sınaqdan sonra
/// cavab açarı və fənn üzrə bal göstərilə bilsin.
/// </summary>
public class ExamAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AttemptId { get; set; }
    public ExamAttempt? Attempt { get; set; }

    public Guid QuestionId { get; set; }
    public ExamQuestion? Question { get; set; }

    /// <summary>Seçilmiş variantın indeksi. Sual cavabsız buraxılıbsa null.</summary>
    public int? SelectedIndex { get; set; }

    public bool IsCorrect { get; set; }
}
