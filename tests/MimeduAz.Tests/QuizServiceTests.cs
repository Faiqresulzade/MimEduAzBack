using FluentAssertions;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Quizzes;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;
using MimeduAz.Infrastructure.Persistence;
using MimeduAz.Tests.TestSupport;

namespace MimeduAz.Tests;

public sealed class QuizServiceTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();

    [Fact]
    public async Task Submit_with_all_correct_answers_passes_and_issues_certificate()
    {
        await using var db = TestHarness.CreateDb();
        var quiz = await SeedQuizAsync(db, passPercent: 70);
        var sut = CreateSut(db, StudentId);

        var result = await sut.SubmitAsync(quiz.Id, AllCorrect(quiz), CancellationToken.None);

        result.Passed.Should().BeTrue();
        result.ScorePercent.Should().Be(100);
        result.CorrectCount.Should().Be(4);
        result.TotalQuestions.Should().Be(4);
        result.CertificateCode.Should().MatchRegex(@"^MIM-\d{4}-\d{4,6}$");

        db.Certificates.Should().ContainSingle(c => c.ResourceQuizAttemptId == result.AttemptId);
    }

    [Fact]
    public async Task Submit_below_pass_percent_fails_and_issues_no_certificate()
    {
        await using var db = TestHarness.CreateDb();
        var quiz = await SeedQuizAsync(db, passPercent: 70);
        var sut = CreateSut(db, StudentId);

        // 4 sualdan yalnız 1-i düzgün -> 25%
        var answers = new SubmitQuizRequest
        {
            Answers = quiz.Questions
                .OrderBy(q => q.OrderIndex)
                .Select((q, i) => new QuizAnswerDto(q.Id, i == 0 ? q.CorrectOptionIndex : WrongIndex(q)))
                .ToList()
        };

        var result = await sut.SubmitAsync(quiz.Id, answers, CancellationToken.None);

        result.Passed.Should().BeFalse();
        result.ScorePercent.Should().Be(25);
        result.CertificateCode.Should().BeNull();
        db.Certificates.Should().BeEmpty();
    }

    [Fact]
    public async Task Submit_exactly_at_pass_percent_passes()
    {
        await using var db = TestHarness.CreateDb();
        var quiz = await SeedQuizAsync(db, passPercent: 75);
        var sut = CreateSut(db, StudentId);

        // 4 sualdan 3-ü düzgün -> 75% = keçid balı
        var answers = new SubmitQuizRequest
        {
            Answers = quiz.Questions
                .OrderBy(q => q.OrderIndex)
                .Select((q, i) => new QuizAnswerDto(q.Id, i == 3 ? WrongIndex(q) : q.CorrectOptionIndex))
                .ToList()
        };

        var result = await sut.SubmitAsync(quiz.Id, answers, CancellationToken.None);

        result.ScorePercent.Should().Be(75);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task GetQuestions_never_exposes_the_correct_option_index()
    {
        await using var db = TestHarness.CreateDb();
        var quiz = await SeedQuizAsync(db, passPercent: 70);

        var questions = await CreateSut(db, StudentId).GetQuestionsAsync(quiz.Id, CancellationToken.None);

        questions.Should().HaveCount(4);
        // QuizQuestionDto-da correctOptionIndex xassəsi ümumiyyətlə yoxdur.
        typeof(QuizQuestionDto).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain("CorrectOptionIndex");
    }

    [Fact]
    public async Task Only_resource_owner_or_admin_can_create_quiz()
    {
        await using var db = TestHarness.CreateDb();
        var resource = await SeedResourceAsync(db);

        var strangerSut = CreateSut(db, Guid.NewGuid());
        var request = new CreateQuizRequest
        {
            PassPercent = 70,
            Mode = "replace",
            Questions =
            {
                new CreateQuizQuestionRequest
                {
                    QuestionText = "Sual",
                    Options = new List<string> { "A", "B" },
                    CorrectOptionIndex = 0
                }
            }
        };

        var act = () => strangerSut.CreateOrUpdateAsync(resource.Id, request, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();

        // Müəllif üçün eyni sorğu işləməlidir.
        var ownerResult = await CreateSut(db, AuthorId).CreateOrUpdateAsync(resource.Id, request, CancellationToken.None);
        ownerResult.QuestionCount.Should().Be(1);
    }

    [Fact]
    public async Task Replace_mode_clears_existing_questions_append_mode_keeps_them()
    {
        await using var db = TestHarness.CreateDb();
        var quiz = await SeedQuizAsync(db, passPercent: 70);
        var sut = CreateSut(db, AuthorId);

        var newQuestion = new CreateQuizRequest
        {
            PassPercent = 80,
            Mode = "append",
            Questions =
            {
                new CreateQuizQuestionRequest
                {
                    QuestionText = "Əlavə sual",
                    Options = new List<string> { "A", "B" },
                    CorrectOptionIndex = 1
                }
            }
        };

        var appended = await sut.CreateOrUpdateAsync(quiz.ResourceId, newQuestion, CancellationToken.None);
        appended.QuestionCount.Should().Be(5);
        appended.PassPercent.Should().Be(80);

        newQuestion.Mode = "replace";
        var replaced = await sut.CreateOrUpdateAsync(quiz.ResourceId, newQuestion, CancellationToken.None);
        replaced.QuestionCount.Should().Be(1);
    }

    [Fact]
    public async Task Submit_requires_authentication()
    {
        await using var db = TestHarness.CreateDb();
        var quiz = await SeedQuizAsync(db, passPercent: 70);

        var act = () => CreateSut(db, userId: null).SubmitAsync(quiz.Id, AllCorrect(quiz), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    private static QuizService CreateSut(ApplicationDbContext db, Guid? userId)
    {
        var currentUser = new FakeCurrentUserService(userId);
        var certificates = new CertificateService(db, currentUser, new FakeCertificateDocumentService(), TestHarness.Logger<CertificateService>());
        return new QuizService(db, currentUser, certificates, TestHarness.Logger<QuizService>());
    }

    private static SubmitQuizRequest AllCorrect(Quiz quiz) => new()
    {
        Answers = quiz.Questions
            .Select(q => new QuizAnswerDto(q.Id, q.CorrectOptionIndex))
            .ToList()
    };

    private static int WrongIndex(QuizQuestion q) => (q.CorrectOptionIndex + 1) % q.Options.Count;

    private static async Task<Resource> SeedResourceAsync(ApplicationDbContext db)
    {
        db.Users.AddRange(
            new ApplicationUser { Id = AuthorId, FullName = "Müəllif", Email = "a@mimedu.az" },
            new ApplicationUser { Id = StudentId, FullName = "İştirakçı", Email = "s@mimedu.az" });

        var resource = new Resource
        {
            Name = "Kəsrlər üzrə iş vərəqi",
            Subject = "Riyaziyyat",
            Grade = 5,
            Type = ResourceType.WorkSheet,
            AuthorId = AuthorId,
            Status = ResourceStatus.Approved
        };

        db.Resources.Add(resource);
        await db.SaveChangesAsync();
        return resource;
    }

    private static async Task<Quiz> SeedQuizAsync(ApplicationDbContext db, int passPercent)
    {
        var resource = await SeedResourceAsync(db);

        var quiz = new Quiz
        {
            ResourceId = resource.Id,
            PassPercent = passPercent,
            Questions =
            {
                Question(0, "1/2 + 1/4 = ?", 1),
                Question(1, "Düzgün kəsr hansıdır?", 2),
                Question(2, "3/6 ixtisar olunmuş forması?", 0),
                Question(3, "0,25 adi kəsrlə?", 1)
            }
        };

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();
        return quiz;
    }

    private static QuizQuestion Question(int order, string text, int correct) => new()
    {
        OrderIndex = order,
        QuestionText = text,
        Options = new List<string> { "A", "B", "C", "D" },
        CorrectOptionIndex = correct
    };
}
