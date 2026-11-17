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

        // AzeriCard ORDER: callback bu nömrə ilə sifarişi tapır.
        builder.Property(o => o.PaymentNumber).HasMaxLength(32);
        builder.HasIndex(o => o.PaymentNumber).IsUnique();

        builder.Property(o => o.CardMask).HasMaxLength(32);
        builder.Property(o => o.Rrn).HasMaxLength(20);
        builder.Property(o => o.IntRef).HasMaxLength(64);
        // Bir bank əməliyyatı yalnız bir sifarişi ödəyə bilər (təkrar istifadə olunmuş callback-ə qarşı).
        builder.HasIndex(o => o.IntRef).IsUnique();
        builder.Property(o => o.ApprovalCode).HasMaxLength(10);
        builder.Property(o => o.PaymentResult).HasMaxLength(100);
        builder.Property(o => o.TermsVersion).HasMaxLength(20);

        // Status yoxlaması fon işi Pending sifarişləri tarixə görə seçir.
        builder.HasIndex(o => new { o.Status, o.CreatedAt });

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

public sealed class PaymentLogConfiguration : IEntityTypeConfiguration<PaymentLog>
{
    public void Configure(EntityTypeBuilder<PaymentLog> builder)
    {
        builder.ToTable("payment_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Kind).IsRequired().HasMaxLength(20);
        builder.Property(l => l.Payload).IsRequired();
        builder.HasIndex(l => l.OrderId);
        builder.HasIndex(l => l.CreatedAt);
    }
}

public sealed class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
{
    public void Configure(EntityTypeBuilder<PayoutRequest> builder)
    {
        builder.ToTable("payout_requests");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasPrecision(10, 2);
        builder.Property(p => p.Iban).IsRequired().HasMaxLength(34);
        builder.Property(p => p.AccountHolder).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Fin).IsRequired().HasMaxLength(7);
        builder.Property(p => p.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.AdminNote).HasMaxLength(500);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.UserId, p.Status });
    }
}
