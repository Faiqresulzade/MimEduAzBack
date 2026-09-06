using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);
        builder.Property(u => u.Subject).HasMaxLength(100);
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasIndex(u => u.FullName);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => t.Token).IsUnique();

        builder.Property(t => t.ReplacedByToken).HasMaxLength(200);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // IsActive hesablanan xassədir, DB-də sütunu yoxdur.
        builder.Ignore(t => t.IsActive);
    }
}
