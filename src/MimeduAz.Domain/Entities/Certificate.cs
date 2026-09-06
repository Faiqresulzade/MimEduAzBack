namespace MimeduAz.Domain.Entities;

/// <summary>Verilmiş sertifikat. Kod ilə publik doğrulana bilir.</summary>
public class Certificate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unikal doğrulama kodu, məs. "MIM-2026-4417".</summary>
    public string Code { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>Təlim sertifikatıdırsa doldurulur.</summary>
    public Guid? TrainingId { get; set; }
    public Training? Training { get; set; }

    /// <summary>Resurs imtahanı sertifikatıdırsa doldurulur.</summary>
    public Guid? ResourceQuizAttemptId { get; set; }
    public QuizAttempt? ResourceQuizAttempt { get; set; }

    /// <summary>İnsan-oxunaqlı təsvir, məs. "Nigar Əliyeva · «Süni intellektlə dərs dizaynı» · 12 saat · 14.03.2026".</summary>
    public string Description { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
