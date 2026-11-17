using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>
/// Müəllifin qazancının bank köçürməsi ilə çıxarılması tələbi.
/// Kart nömrəsi toplanmır — yalnız IBAN, ad-soyad və FİN.
/// </summary>
public class PayoutRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public decimal Amount { get; set; }

    /// <summary>AZ IBAN, boşluqsuz böyük hərflə (28 simvol).</summary>
    public string Iban { get; set; } = string.Empty;

    /// <summary>Hesab sahibinin adı (bank hesabındakı kimi).</summary>
    public string AccountHolder { get; set; } = string.Empty;

    /// <summary>Şəxsiyyət vəsiqəsinin FİN kodu (7 simvol).</summary>
    public string Fin { get; set; } = string.Empty;

    public PayoutStatus Status { get; set; } = PayoutStatus.Requested;

    /// <summary>Admin qeydi: köçürmənin referansı və ya rədd səbəbi.</summary>
    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
