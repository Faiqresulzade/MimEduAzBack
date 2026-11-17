using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>
/// Sifariş. Pulsuz səbət dərhal <see cref="OrderStatus.Paid"/> olur; ödənişli sifariş
/// <see cref="OrderStatus.Pending"/> yaradılır və AzeriCard təsdiqindən sonra ödənilmiş sayılır.
/// Kart məlumatı (PAN, CVC) heç vaxt saxlanmır — yalnız bankın qaytardığı maskalı nömrə.
/// </summary>
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>İnsan-oxunaqlı unikal kod, məs. "MIM-7113".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// AzeriCard-a göndərilən ORDER: yalnız rəqəm, 6–32 simvol, unikal.
    /// Pulsuz sifarişlərdə null.
    /// </summary>
    public string? PaymentNumber { get; set; }

    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>Sifariş anındakı ad snapshot-ı. Login olmayıbsa "Qonaq".</summary>
    public string CustomerName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    /// <summary>Platformanın komissiya payı (ödənişli resurslardan).</summary>
    public decimal CommissionAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    /// <summary>Bankın cavabından: maskalı kart (məs. 416973******5678), RRN, INT_REF, təsdiq kodu.</summary>
    public string? CardMask { get; set; }
    public string? Rrn { get; set; }
    public string? IntRef { get; set; }
    public string? ApprovalCode { get; set; }

    /// <summary>Bankın son cavab kodu (ACTION/RC) — uğursuz ödənişin səbəbi üçün.</summary>
    public string? PaymentResult { get; set; }

    /// <summary>Alıcının ödənişdən əvvəl şərtlərlə razılaşdığı an və şərtlərin versiyası.</summary>
    public DateTime? TermsAcceptedAt { get; set; }
    public string? TermsVersion { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
