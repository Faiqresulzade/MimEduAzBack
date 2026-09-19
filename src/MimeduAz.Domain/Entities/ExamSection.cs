namespace MimeduAz.Domain.Entities;

/// <summary>
/// Sınağın fənn bölməsi, məs. "Riyaziyyat — 30 sual".
/// Nəticə həm ümumi, həm də bölmə üzrə ayrıca hesablanır.
/// </summary>
public class ExamSection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>Bölmənin fənni, məs. "Riyaziyyat".</summary>
    public string Subject { get; set; } = string.Empty;

    public ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();
}
