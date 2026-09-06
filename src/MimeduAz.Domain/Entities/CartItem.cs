using MimeduAz.Domain.Enums;

namespace MimeduAz.Domain.Entities;

/// <summary>Səbətdəki bir məhsul (resurs və ya təlim).</summary>
public class CartItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CartId { get; set; }
    public Cart? Cart { get; set; }

    public CatalogItemType ItemType { get; set; }

    /// <summary><see cref="Resource"/>.Id və ya <see cref="Training"/>.Id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Səbətə əlavə olunduğu andakı qiymət.</summary>
    public decimal Price { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
