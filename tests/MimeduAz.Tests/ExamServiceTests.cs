using FluentAssertions;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Exams;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

/// <summary>
/// Sınağın vaxt limiti və qiymətləndirmə məntiqi. Vaxtın bitməsi burada
/// <c>ExpiresAt</c> dəyişdirilərək yoxlanılır — skript testi gözləməli olardı.
/// </summary>
public sealed class ExamServiceTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();

    [Fact]
    public async Task Starting_an_exam_records_the_deadline_on_the_server()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db, durationMinutes: 30);

        var run = await CreateSut(db, StudentId).StartAsync(exam.Id, CancellationToken.None);

        run.DurationMinutes.Should().Be(30);
        run.RemainingSeconds.Should().BeInRange(1770, 1800);
        run.ExpiresAt.Should().BeCloseTo(run.StartedAt.AddMinutes(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Refreshing_the_page_continues_the_same_attempt_without_resetting_the_timer()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var first = await sut.StartAsync(exam.Id, CancellationToken.None);
        var second = await sut.StartAsync(exam.Id, CancellationToken.None);

        second.AttemptId.Should().Be(first.AttemptId);
        second.ExpiresAt.Should().Be(first.ExpiresAt);
        db.ExamAttempts.Count().Should().Be(1);
    }

    [Fact]
    public async Task Questions_never_expose_the_correct_answer_while_the_exam_is_running()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);

        var run = await CreateSut(db, StudentId).StartAsync(exam.Id, CancellationToken.None);

        // ExamRunQuestionDto-da düzgün cavab sahəsi ümumiyyətlə yoxdur -
        // burada variantların gəldiyini və sualın tam olduğunu yoxlayırıq.
        var question = run.Sections.Single().Questions.First();
        question.Options.Should().HaveCount(4);
        question.QuestionText.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Unanswered_questions_count_as_wrong()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);
        var questions = run.Sections.Single().Questions;

        // 4 sualdan yalnız birini düzgün cavablandırırıq, qalanı göndərilmir.
        var result = await sut.SubmitAsync(run.AttemptId, new SubmitExamRequest
        {
            Answers = new List<SubmitExamAnswerRequest>
            {
                new() { QuestionId = questions[0].Id, SelectedIndex = 0 }
            }
        }, CancellationToken.None);

        result.CorrectCount.Should().Be(1);
        result.QuestionCount.Should().Be(4);
        result.ScorePercent.Should().Be(25);
        result.Passed.Should().BeFalse();
        result.CertificateCode.Should().BeNull();
    }

    [Fact]
    public async Task Explicitly_null_answers_are_accepted_and_scored_as_wrong()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);
        var questions = run.Sections.Single().Questions;

        var result = await sut.SubmitAsync(run.AttemptId, new SubmitExamRequest
        {
            Answers = questions
                .Select(q => new SubmitExamAnswerRequest { QuestionId = q.Id, SelectedIndex = null })
                .ToList()
        }, CancellationToken.None);

        result.ScorePercent.Should().Be(0);
        result.Sections.Single().Questions.Should().OnlyContain(q => !q.IsCorrect);
    }

    [Fact]
    public async Task Passing_the_threshold_issues_a_certificate_automatically()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db, passPercent: 50);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);

        var result = await sut.SubmitAsync(run.AttemptId, AllCorrect(run), CancellationToken.None);

        result.ScorePercent.Should().Be(100);
        result.Passed.Should().BeTrue();
        result.CertificateCode.Should().NotBeNullOrWhiteSpace();

        db.Certificates.Count(c => c.ExamAttemptId == run.AttemptId).Should().Be(1);
    }

    [Fact]
    public async Task Submitting_after_the_deadline_is_rejected_and_closes_the_attempt()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);

        // Vaxtı süni şəkildə keçmişə çəkirik - güzəşt müddətindən də çox.
        var attempt = db.ExamAttempts.Single(a => a.Id == run.AttemptId);
        attempt.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var act = () => sut.SubmitAsync(run.AttemptId, AllCorrect(run), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();

        var closed = db.ExamAttempts.Single(a => a.Id == run.AttemptId);
        closed.Status.Should().Be(ExamAttemptStatus.Expired);
        closed.ScorePercent.Should().Be(0);
        closed.Passed.Should().BeFalse();
        db.Certificates.Any(c => c.ExamAttemptId == run.AttemptId).Should().BeFalse();
    }

    [Fact]
    public async Task A_few_seconds_of_network_delay_still_counts_as_on_time()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);

        // Sayğac yeni sıfırlanıb: güzəşt müddəti (30 san) daxilində cavab itməməlidir.
        var attempt = db.ExamAttempts.Single(a => a.Id == run.AttemptId);
        attempt.ExpiresAt = DateTime.UtcNow.AddSeconds(-5);
        await db.SaveChangesAsync();

        var result = await sut.SubmitAsync(run.AttemptId, AllCorrect(run), CancellationToken.None);

        result.Status.Should().Be(ExamAttemptStatus.Submitted);
        result.ScorePercent.Should().Be(100);
    }

    [Fact]
    public async Task An_expired_attempt_cannot_be_resumed()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);

        var attempt = db.ExamAttempts.Single(a => a.Id == run.AttemptId);
        attempt.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var act = () => sut.StartAsync(exam.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        db.ExamAttempts.Single().Status.Should().Be(ExamAttemptStatus.Expired);
    }

    [Fact]
    public async Task A_paid_exam_cannot_be_started_without_buying_it()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db, isPaid: true, price: 10m);

        var act = () => CreateSut(db, StudentId).StartAsync(exam.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task An_unapproved_exam_cannot_be_started()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db, status: ExamStatus.Pending);

        var act = () => CreateSut(db, StudentId).StartAsync(exam.Id, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Section_scores_are_reported_separately_per_subject()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedTwoSectionExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);

        var math = run.Sections.Single(s => s.Subject == "Riyaziyyat");
        var physics = run.Sections.Single(s => s.Subject == "Fizika");

        // Riyaziyyatı düzgün, fizikanı səhv cavablandırırıq.
        var answers = math.Questions
            .Select(q => new SubmitExamAnswerRequest { QuestionId = q.Id, SelectedIndex = 0 })
            .Concat(physics.Questions
                .Select(q => new SubmitExamAnswerRequest { QuestionId = q.Id, SelectedIndex = 1 }))
            .ToList();

        var result = await sut.SubmitAsync(
            run.AttemptId, new SubmitExamRequest { Answers = answers }, CancellationToken.None);

        result.Sections.Single(s => s.Subject == "Riyaziyyat").ScorePercent.Should().Be(100);
        result.Sections.Single(s => s.Subject == "Fizika").ScorePercent.Should().Be(0);
        result.ScorePercent.Should().Be(50);
    }

    [Fact]
    public async Task Answers_for_questions_from_another_exam_are_ignored()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);
        var sut = CreateSut(db, StudentId);

        var run = await sut.StartAsync(exam.Id, CancellationToken.None);

        var answers = AllCorrect(run).Answers;
        answers.Add(new SubmitExamAnswerRequest { QuestionId = Guid.NewGuid(), SelectedIndex = 0 });

        var result = await sut.SubmitAsync(
            run.AttemptId, new SubmitExamRequest { Answers = answers }, CancellationToken.None);

        result.QuestionCount.Should().Be(4);
        result.ScorePercent.Should().Be(100);
    }

    [Fact]
    public async Task The_author_cannot_sit_their_own_exam()
    {
        await using var db = TestHarness.CreateDb();
        var exam = await SeedExamAsync(db);

        var act = () => CreateSut(db, TeacherId).StartAsync(exam.Id, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    /// <summary>Seed edilən sınaqda düzgün cavab həmişə 0-cı variantdır.</summary>
    private static SubmitExamRequest AllCorrect(ExamRunDto run) => new()
    {
        Answers = run.Sections
            .SelectMany(s => s.Questions)
            .Select(q => new SubmitExamAnswerRequest { QuestionId = q.Id, SelectedIndex = 0 })
            .ToList()
    };

    private static ExamService CreateSut(ApplicationDbContext db, Guid userId, bool isAdmin = false)
    {
        var currentUser = new FakeCurrentUserService(userId, isAdmin);

        return new ExamService(
            db,
            currentUser,
            new FakeFileStorageService(),
            new CertificateService(
                db,
                currentUser,
                new FakeCertificateDocumentService(),
                TestHarness.Logger<CertificateService>()),
            new FakeNotificationService(),
            TestHarness.FileStorage(),
            TestHarness.Logger<ExamService>());
    }

    private static async Task<Exam> SeedExamAsync(
        ApplicationDbContext db,
        int durationMinutes = 10,
        int passPercent = 60,
        bool isPaid = false,
        decimal price = 0m,
        ExamStatus status = ExamStatus.Approved)
    {
        db.Users.Add(new ApplicationUser { Id = TeacherId, FullName = "Nigar Əliyeva", Email = "n@mimedu.az" });
        db.Users.Add(new ApplicationUser { Id = StudentId, FullName = "Sevinc Quliyeva", Email = "s@mimedu.az" });

        var exam = new Exam
        {
            Name = "Riyaziyyat sınağı",
            Description = "Təsvir",
            Subject = "Riyaziyyat",
            Grade = 6,
            AuthorId = TeacherId,
            DurationMinutes = durationMinutes,
            PassPercent = passPercent,
            IsPaid = isPaid,
            Price = price,
            Status = status,
            ApprovedAt = status == ExamStatus.Approved ? DateTime.UtcNow : null
        };

        var section = new ExamSection { ExamId = exam.Id, OrderIndex = 0, Subject = "Riyaziyyat" };

        for (var i = 0; i < 4; i++)
        {
            section.Questions.Add(new ExamQuestion
            {
                SectionId = section.Id,
                OrderIndex = i,
                QuestionText = $"Sual {i + 1}",
                Options = new List<string> { "Düzgün", "Səhv 1", "Səhv 2", "Səhv 3" },
                CorrectOptionIndex = 0
            });
        }

        exam.Sections.Add(section);
        db.Exams.Add(exam);
        await db.SaveChangesAsync();

        return exam;
    }

    private static async Task<Exam> SeedTwoSectionExamAsync(ApplicationDbContext db)
    {
        db.Users.Add(new ApplicationUser { Id = TeacherId, FullName = "Nigar Əliyeva", Email = "n@mimedu.az" });
        db.Users.Add(new ApplicationUser { Id = StudentId, FullName = "Sevinc Quliyeva", Email = "s@mimedu.az" });

        var exam = new Exam
        {
            Name = "Buraxılış sınağı",
            Description = "Təsvir",
            Subject = "Buraxılış",
            Grade = 11,
            AuthorId = TeacherId,
            DurationMinutes = 60,
            PassPercent = 60,
            Status = ExamStatus.Approved,
            ApprovedAt = DateTime.UtcNow
        };

        foreach (var (subject, index) in new[] { ("Riyaziyyat", 0), ("Fizika", 1) })
        {
            var section = new ExamSection { ExamId = exam.Id, OrderIndex = index, Subject = subject };

            for (var i = 0; i < 2; i++)
            {
                section.Questions.Add(new ExamQuestion
                {
                    SectionId = section.Id,
                    OrderIndex = i,
                    QuestionText = $"{subject} sualı {i + 1}",
                    Options = new List<string> { "Düzgün", "Səhv" },
                    CorrectOptionIndex = 0
                });
            }

            exam.Sections.Add(section);
        }

        db.Exams.Add(exam);
        await db.SaveChangesAsync();

        return exam;
    }
}
