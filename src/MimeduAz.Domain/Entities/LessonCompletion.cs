namespace MimeduAz.Domain.Entities;

/// <summary>
/// İstifadəçinin bir dərsi tamamladığını göstərən qeyd.
/// Enrollment-ə bağlıdır ki, eyni təlimi yenidən alan istifadəçidə qarışıqlıq olmasın.
/// </summary>
public class LessonCompletion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EnrollmentId { get; set; }
    public Enrollment? Enrollment { get; set; }

    public Guid LessonId { get; set; }
    public TrainingLesson? Lesson { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
