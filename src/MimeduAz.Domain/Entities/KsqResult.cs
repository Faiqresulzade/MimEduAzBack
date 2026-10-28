namespace MimeduAz.Domain.Entities;

/// <summary>Bir şagirdin yoxlanmış KSQ cavab kartı. Test silinəndə nəticələri də silinir.</summary>
public class KsqResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public Guid TestId { get; set; }
    public KsqTest? Test { get; set; }

    /// <summary>Saxlanma anındakı test adı — test sonradan adlandırılsa da nəticə dəyişmir.</summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>Sinif, məs. "9A". Boş ola bilər.</summary>
    public string Sinif { get; set; } = string.Empty;

    /// <summary>Şagirdin adı.</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>"A" və ya "B".</summary>
    public string Variant { get; set; } = "A";

    /// <summary>Kartdakı şagird kodu (varsa).</summary>
    public string Kod { get; set; } = string.Empty;

    /// <summary>Qapalı suallara verilmiş cavablar ("A".."E" və ya boş üçün null).</summary>
    public List<string?> Answers { get; set; } = new();

    /// <summary>Müəllimin açıq suallara verdiyi ballar.</summary>
    public List<decimal> OpenScores { get; set; } = new();

    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }

    /// <summary>Səhv və ya boş buraxılmış qapalı sualların nömrələri (1-dən başlayır).</summary>
    public List<int> WrongQuestions { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
