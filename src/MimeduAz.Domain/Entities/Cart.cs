namespace MimeduAz.Domain.Entities;

/// <summary>İstifadəçinin səbəti. Hər istifadəçinin bir aktiv səbəti olur.</summary>
public class Cart
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
