namespace MimeduAz.Domain.Entities;

/// <summary>İstifadəçinin imtahan cəhdi.</summary>
public class QuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public int ScorePercent { get; set; }
    public bool Passed { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    public Certificate? Certificate { get; set; }
}
