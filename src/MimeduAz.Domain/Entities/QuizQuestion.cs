namespace MimeduAz.Domain.Entities;

/// <summary>Quiz sualı. Variantlar JSON sütununda saxlanılır (MySQL - massiv tipi olmadığı üçün).</summary>
public class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public int OrderIndex { get; set; }
    public string QuestionText { get; set; } = string.Empty;

    public List<string> Options { get; set; } = new();

    /// <summary>Düzgün variantın <see cref="Options"/> içindəki indeksi. API cavablarında HEÇ VAXT göndərilmir.</summary>
    public int CorrectOptionIndex { get; set; }
}
