using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>Sifariş sətri. Adı və qiyməti sifariş anından snapshot kimi saxlanılır.</summary>
public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public CatalogItemType ItemType { get; set; }
    public Guid ItemId { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>Bu sətirdən platformaya düşən komissiya (resurslarda 20%, təlimlərdə 0).</summary>
    public decimal CommissionAmount { get; set; }

    /// <summary>Resurs müəllifinə düşən pay. Təlimlərdə 0.</summary>
    public decimal AuthorPayoutAmount { get; set; }
}
