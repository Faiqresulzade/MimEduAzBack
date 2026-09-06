using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;
using MimeduAz.Infrastructure.Persistence.Conversions;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Subject).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Grade).IsRequired();
        builder.Property(r => r.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Price).HasPrecision(10, 2);
        builder.Property(r => r.Downloads).HasDefaultValue(0);
        builder.Property(r => r.FilePath).HasMaxLength(500);
        builder.Property(r => r.OriginalFileName).HasMaxLength(260);
        builder.Property(r => r.RejectionReason).HasMaxLength(1000);

        builder.HasOne(r => r.Author)
            .WithMany(u => u.Resources)
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Resurs Bankının əsas filtri: status + fənn + sinif.
        builder.HasIndex(r => new { r.Status, r.Subject, r.Grade });
        builder.HasIndex(r => r.AuthorId);
    }
}

public sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("blog_posts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title).IsRequired().HasMaxLength(250);
        builder.Property(p => p.Tag).IsRequired().HasMaxLength(60);
        builder.Property(p => p.ReadTime).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Excerpt).IsRequired().HasMaxLength(600);

        // MySQL-də doğma massiv tipi yoxdur - paraqraflar JSON mətn sütununda saxlanılır.
        builder.Property(p => p.Body)
            .HasConversion(StringListJsonConverter.Converter, StringListJsonConverter.Comparer)
            .HasColumnType("json");
    }
}
