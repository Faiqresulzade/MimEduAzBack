using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>Müəllimin təlimə yazılması.</summary>
public class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public Guid TrainingId { get; set; }
    public Training? Training { get; set; }

    public int ProgressPercent { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.InProgress;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}
