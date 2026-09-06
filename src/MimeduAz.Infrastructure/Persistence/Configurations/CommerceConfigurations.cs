using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hər istifadəçinin yalnız bir aktiv səbəti olur.
        builder.HasIndex(c => c.UserId).IsUnique();
    }
}

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Price).HasPrecision(10, 2);

        builder.HasIndex(i => new { i.CartId, i.ItemType, i.ItemId }).IsUnique();
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Code).IsRequired().HasMaxLength(30);
        builder.HasIndex(o => o.Code).IsUnique();

        builder.Property(o => o.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(o => o.TotalAmount).HasPrecision(10, 2);
        builder.Property(o => o.CommissionAmount).HasPrecision(10, 2);
        builder.Property(o => o.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.CreatedAt);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Price).HasPrecision(10, 2);
        builder.Property(i => i.CommissionAmount).HasPrecision(10, 2);
        builder.Property(i => i.AuthorPayoutAmount).HasPrecision(10, 2);

        builder.HasIndex(i => new { i.ItemType, i.ItemId });
    }
}
