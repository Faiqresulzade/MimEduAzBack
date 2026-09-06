using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;
using MimeduAz.Infrastructure.Persistence.Conversions;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.PassPercent).HasDefaultValue(70);

        // Hər resursun ən çoxu bir imtahanı ola bilər.
        builder.HasOne(q => q.Resource)
            .WithOne(r => r.Quiz)
            .HasForeignKey<Quiz>(q => q.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.ResourceId).IsUnique();

        builder.HasMany(q => q.Questions)
            .WithOne(x => x.Quiz)
            .HasForeignKey(x => x.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Attempts)
            .WithOne(a => a.Quiz)
            .HasForeignKey(a => a.QuizId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("quiz_questions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuestionText).IsRequired().HasMaxLength(500);

        // MySQL-də doğma massiv tipi yoxdur - variantlar JSON mətn sütununda saxlanılır.
        builder.Property(q => q.Options)
            .HasConversion(StringListJsonConverter.Converter, StringListJsonConverter.Comparer)
            .HasColumnType("json");

        builder.HasIndex(q => new { q.QuizId, q.OrderIndex });
    }
}

public sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("quiz_attempts");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.UserId, a.QuizId });
    }
}

public sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("certificates");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(30);
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.Description).IsRequired().HasMaxLength(500);

        builder.HasOne(c => c.User)
            .WithMany(u => u.Certificates)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Training)
            .WithMany()
            .HasForeignKey(c => c.TrainingId)
            .OnDelete(DeleteBehavior.SetNull);

        // Bir imtahan cəhdi üçün yalnız bir sertifikat verilə bilər.
        builder.HasOne(c => c.ResourceQuizAttempt)
            .WithOne(a => a.Certificate)
            .HasForeignKey<Certificate>(c => c.ResourceQuizAttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
