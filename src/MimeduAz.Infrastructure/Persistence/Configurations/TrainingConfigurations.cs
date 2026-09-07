using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Persistence.Configurations;

public sealed class TrainingConfiguration : IEntityTypeConfiguration<Training>
{
    public void Configure(EntityTypeBuilder<Training> builder)
    {
        builder.ToTable("trainings");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Format).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);
        builder.Property(t => t.Price).HasPrecision(10, 2);
        builder.Property(t => t.MetaLabel).IsRequired().HasMaxLength(100);

        builder.HasMany(t => t.SyllabusItems)
            .WithOne(s => s.Training)
            .HasForeignKey(s => s.TrainingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Lessons)
            .WithOne(l => l.Training)
            .HasForeignKey(l => l.TrainingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TrainingLessonConfiguration : IEntityTypeConfiguration<TrainingLesson>
{
    public void Configure(EntityTypeBuilder<TrainingLesson> builder)
    {
        builder.ToTable("training_lessons");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Title).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Description).IsRequired().HasMaxLength(2000);
        builder.Property(l => l.VideoUrl).HasMaxLength(1000);

        builder.HasIndex(l => new { l.TrainingId, l.OrderIndex });
    }
}

public sealed class LessonCompletionConfiguration : IEntityTypeConfiguration<LessonCompletion>
{
    public void Configure(EntityTypeBuilder<LessonCompletion> builder)
    {
        builder.ToTable("lesson_completions");
        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.Enrollment)
            .WithMany(e => e.CompletedLessons)
            .HasForeignKey(c => c.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Lesson)
            .WithMany(l => l.Completions)
            .HasForeignKey(c => c.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Eyni dərs bir enrollment üçün yalnız bir dəfə tamamlana bilər.
        builder.HasIndex(c => new { c.EnrollmentId, c.LessonId }).IsUnique();
    }
}

public sealed class TrainingSyllabusItemConfiguration : IEntityTypeConfiguration<TrainingSyllabusItem>
{
    public void Configure(EntityTypeBuilder<TrainingSyllabusItem> builder)
    {
        builder.ToTable("training_syllabus_items");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Text).IsRequired().HasMaxLength(500);
        builder.HasIndex(s => new { s.TrainingId, s.OrderIndex });
    }
}

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ProgressPercent).HasDefaultValue(0);

        builder.HasOne(e => e.User)
            .WithMany(u => u.Enrollments)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Training)
            .WithMany(t => t.Enrollments)
            .HasForeignKey(e => e.TrainingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Eyni istifadəçi eyni təlimə iki dəfə yazıla bilməz.
        builder.HasIndex(e => new { e.UserId, e.TrainingId }).IsUnique();
    }
}
