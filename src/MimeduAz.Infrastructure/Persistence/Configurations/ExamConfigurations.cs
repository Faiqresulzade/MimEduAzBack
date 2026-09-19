using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.Property(e => e.PassPercent).HasDefaultValue(60);
        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);

        builder.HasOne(e => e.Author)
            .WithMany()
            .HasForeignKey(e => e.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Sections)
            .WithOne(s => s.Exam)
            .HasForeignKey(s => s.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Attempts)
            .WithOne(a => a.Exam)
            .HasForeignKey(a => a.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.Status, e.Subject });
    }
}

public sealed class ExamSectionConfiguration : IEntityTypeConfiguration<ExamSection>
{
    public void Configure(EntityTypeBuilder<ExamSection> builder)
    {
        builder.ToTable("exam_sections");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Subject).IsRequired().HasMaxLength(100);

        builder.HasMany(s => s.Questions)
            .WithOne(q => q.Section)
            .HasForeignKey(q => q.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ExamId, s.OrderIndex });
    }
}

public sealed class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.ToTable("exam_questions");
        builder.HasKey(q => q.Id);

        // Şəkildən ibarət sualda mətn boş qala bilər, ona görə IsRequired yoxdur.
        // LaTeX ifadələri uzun ola bildiyi üçün limit quiz suallarından genişdir.
        builder.Property(q => q.QuestionText).HasMaxLength(2000);
        builder.Property(q => q.ImagePath).HasMaxLength(400);

        // Variantlar Postgres-in doğma text[] massivində saxlanılır.
        builder.Property(q => q.Options).HasColumnType("text[]");

        builder.HasMany(q => q.Answers)
            .WithOne(a => a.Question)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.SectionId, q.OrderIndex });
    }
}

public sealed class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ToTable("exam_attempts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Answers)
            .WithOne(x => x.Attempt)
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir istifadəçi bir sınağı bir dəfə verir - təkrar cəhd bu indekslə də qorunur.
        builder.HasIndex(a => new { a.UserId, a.ExamId }).IsUnique();
    }
}

public sealed class ExamAnswerConfiguration : IEntityTypeConfiguration<ExamAnswer>
{
    public void Configure(EntityTypeBuilder<ExamAnswer> builder)
    {
        builder.ToTable("exam_answers");
        builder.HasKey(a => a.Id);

        // Eyni sual bir cəhddə yalnız bir dəfə cavablandırıla bilər.
        builder.HasIndex(a => new { a.AttemptId, a.QuestionId }).IsUnique();
    }
}
