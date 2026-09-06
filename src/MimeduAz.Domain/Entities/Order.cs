using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>Tamamlanmış sifariş. Demo rejimdə birbaşa Paid statusunda yaradılır.</summary>
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>İnsan-oxunaqlı unikal kod, məs. "MIM-7113".</summary>
    public string Code { get; set; } = string.Empty;

    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>Sifariş anındakı ad snapshot-ı. Login olmayıbsa "Qonaq".</summary>
    public string CustomerName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    /// <summary>Platformanın komissiya payı (ödənişli resurslardan).</summary>
    public decimal CommissionAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Paid;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
