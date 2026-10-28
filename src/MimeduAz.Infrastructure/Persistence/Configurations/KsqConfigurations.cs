using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class KsqTestConfiguration : IEntityTypeConfiguration<KsqTest>
{
    public void Configure(EntityTypeBuilder<KsqTest> builder)
    {
        builder.ToTable("ksq_tests");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ClosedPoints).HasJsonConversion();
        builder.Property(t => t.OpenPoints).HasJsonConversion();
        builder.Property(t => t.KeyA).HasJsonConversion();
        builder.Property(t => t.KeyB).HasJsonConversion();

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId);
    }
}

public sealed class KsqResultConfiguration : IEntityTypeConfiguration<KsqResult>
{
    public void Configure(EntityTypeBuilder<KsqResult> builder)
    {
        builder.ToTable("ksq_results");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TestName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Sinif).IsRequired().HasMaxLength(20);
        builder.Property(r => r.Ad).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Variant).IsRequired().HasMaxLength(1);
        builder.Property(r => r.Kod).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Answers).HasJsonConversion();
        builder.Property(r => r.OpenScores).HasJsonConversion();
        builder.Property(r => r.WrongQuestions).HasJsonConversion();
        builder.Property(r => r.Score).HasPrecision(8, 2);
        builder.Property(r => r.MaxScore).HasPrecision(8, 2);

        // Test silinəndə nəticələri də silinir.
        builder.HasOne(r => r.Test)
            .WithMany(t => t.Results)
            .HasForeignKey(r => r.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        // PostgreSQL çoxlu cascade yoluna icazə verir (users -> ksq_tests -> ksq_results və birbaşa).
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Nəticələr səhifəsinin filtri: istifadəçi + test + sinif.
        builder.HasIndex(r => new { r.UserId, r.TestId, r.Sinif });
    }
}

internal static class JsonColumnExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Siyahını PostgreSQL <c>jsonb</c> sütununda saxlayır. Comparer olmadan EF
    /// siyahının daxilindəki dəyişikliyi görmür və UPDATE yazmır.
    /// </summary>
    public static PropertyBuilder<List<T>> HasJsonConversion<T>(this PropertyBuilder<List<T>> builder)
    {
        builder
            .HasConversion(
                list => JsonSerializer.Serialize(list, Options),
                json => JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>(),
                new ValueComparer<List<T>>(
                    (a, b) => a!.SequenceEqual(b!),
                    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    list => list.ToList()))
            .HasColumnType("jsonb")
            .IsRequired();

        return builder;
    }
}
