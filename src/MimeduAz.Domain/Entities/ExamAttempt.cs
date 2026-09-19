using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>
/// İştirakçının sınaq cəhdi. Vaxt sayğacı server tərəfdə saxlanılır:
/// <see cref="ExpiresAt"/> başlanğıcda hesablanır ki, brauzerin saatı ilə oynamaq mümkün olmasın.
/// </summary>
public class ExamAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Cavabların qəbul olunduğu son an (<see cref="StartedAt"/> + sınağın müddəti).</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public ExamAttemptStatus Status { get; set; } = ExamAttemptStatus.InProgress;

    public int ScorePercent { get; set; }
    public int CorrectCount { get; set; }
    public int QuestionCount { get; set; }
    public bool Passed { get; set; }

    public ICollection<ExamAnswer> Answers { get; set; } = new List<ExamAnswer>();

    public Certificate? Certificate { get; set; }
}
