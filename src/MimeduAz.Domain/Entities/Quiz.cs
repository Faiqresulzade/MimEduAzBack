namespace MimeduAz.Domain.Entities;

/// <summary>Resursa bağlı imtahan. Hər resursun ən çoxu bir quiz-i olur.</summary>
public class Quiz
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResourceId { get; set; }
    public Resource? Resource { get; set; }

    /// <summary>Sertifikat almaq üçün lazım olan minimum faiz.</summary>
    public int PassPercent { get; set; } = 70;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
