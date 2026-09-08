using FluentAssertions;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Certificates;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

public sealed class CertificateServiceTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();

    [Fact]
    public async Task Verify_returns_holder_details_for_a_known_code()
    {
        await using var db = TestHarness.CreateDb();
        await SeedCertificateAsync(db, "MIM-2026-4417");

        var result = await CreateSut(db).VerifyAsync("MIM-2026-4417", CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.HolderName.Should().Be("Nigar Əliyeva");
        result.Description.Should().Contain("Süni intellektlə dərs dizaynı");
    }

    [Fact]
    public async Task Verify_is_case_insensitive_and_trims_whitespace()
    {
        await using var db = TestHarness.CreateDb();
        await SeedCertificateAsync(db, "MIM-2026-4417");

        var result = await CreateSut(db).VerifyAsync("  mim-2026-4417  ", CancellationToken.None);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_returns_invalid_instead_of_throwing_for_unknown_codes()
    {
        await using var db = TestHarness.CreateDb();
        await SeedCertificateAsync(db, "MIM-2026-4417");

        var result = await CreateSut(db).VerifyAsync("MIM-2026-0000", CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.HolderName.Should().BeNull();
        result.IssuedAt.Should().BeNull();
    }

    [Fact]
    public async Task Manual_issue_marks_the_enrollment_completed()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingWithEnrollmentAsync(db);

        var certificate = await CreateSut(db).IssueAsync(
            new IssueCertificateRequest { UserFullName = "Nigar Əliyeva", TrainingId = training.Id },
            CancellationToken.None);

        certificate.Code.Should().MatchRegex(@"^MIM-\d{4}-\d{4,6}$");
        certificate.Description.Should().Contain("Nigar Əliyeva");
        certificate.Description.Should().Contain("8 saat");

        var enrollment = db.Enrollments.Single();
        enrollment.Status.Should().Be(EnrollmentStatus.Completed);
        enrollment.ProgressPercent.Should().Be(100);
    }

    [Fact]
    public async Task Manual_issue_fails_for_an_unknown_user()
    {
        await using var db = TestHarness.CreateDb();
        var training = await SeedTrainingWithEnrollmentAsync(db);

        var act = () => CreateSut(db).IssueAsync(
            new IssueCertificateRequest { UserFullName = "Mövcud Olmayan", TrainingId = training.Id },
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Issuing_twice_for_the_same_quiz_attempt_is_idempotent()
    {
        await using var db = TestHarness.CreateDb();
        var attempt = await SeedPassedAttemptAsync(db);
        var sut = CreateSut(db);

        var first = await sut.IssueForQuizAttemptAsync(attempt.Id, CancellationToken.None);
        var second = await sut.IssueForQuizAttemptAsync(attempt.Id, CancellationToken.None);

        second.Code.Should().Be(first.Code);
        db.Certificates.Should().ContainSingle();
    }

    [Fact]
    public async Task Certificate_is_not_issued_for_a_failed_attempt()
    {
        await using var db = TestHarness.CreateDb();
        var attempt = await SeedPassedAttemptAsync(db, passed: false);

        var act = () => CreateSut(db).IssueForQuizAttemptAsync(attempt.Id, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    private static CertificateService CreateSut(ApplicationDbContext db) => new(
        db,
        new FakeCurrentUserService(TeacherId),
        new FakeCertificateDocumentService(),
        TestHarness.Logger<CertificateService>());

    private static async Task<Training> SeedTrainingWithEnrollmentAsync(ApplicationDbContext db)
    {
        db.Users.Add(new ApplicationUser { Id = TeacherId, FullName = "Nigar Əliyeva", Email = "n@mimedu.az" });

        var training = new Training
        {
            Name = "Süni intellektlə dərs dizaynı",
            Format = TrainingFormat.Live,
            Description = "Praktik emalatxana",
            Price = 120m,
            DurationHours = 8,
            MetaLabel = "2 gün · 8 saat",
            SeatLimit = 25
        };
        db.Trainings.Add(training);

        db.Enrollments.Add(new Enrollment
        {
            UserId = TeacherId,
            TrainingId = training.Id,
            ProgressPercent = 60,
            Status = EnrollmentStatus.InProgress
        });

        await db.SaveChangesAsync();
        return training;
    }

    private static async Task SeedCertificateAsync(ApplicationDbContext db, string code)
    {
        var training = await SeedTrainingWithEnrollmentAsync(db);

        db.Certificates.Add(new Certificate
        {
            Code = code,
            UserId = TeacherId,
            TrainingId = training.Id,
            Description = "Nigar Əliyeva · «Süni intellektlə dərs dizaynı» · 8 saat · 14.03.2026",
            IssuedAt = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
    }

    private static async Task<QuizAttempt> SeedPassedAttemptAsync(ApplicationDbContext db, bool passed = true)
    {
        db.Users.Add(new ApplicationUser { Id = TeacherId, FullName = "Nigar Əliyeva", Email = "n@mimedu.az" });

        var resource = new Resource
        {
            Name = "Kəsrlər üzrə iş vərəqi",
            Subject = "Riyaziyyat",
            Grade = 5,
            Type = ResourceType.WorkSheet,
            AuthorId = TeacherId,
            Status = ResourceStatus.Approved
        };
        db.Resources.Add(resource);

        var quiz = new Quiz { ResourceId = resource.Id, PassPercent = 70 };
        db.Quizzes.Add(quiz);

        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            UserId = TeacherId,
            ScorePercent = passed ? 100 : 25,
            Passed = passed
        };
        db.QuizAttempts.Add(attempt);

        await db.SaveChangesAsync();
        return attempt;
    }
}
