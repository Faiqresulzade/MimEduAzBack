using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Contracts.Quizzes;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Services;

public sealed class QuizService : IQuizService
{
    private const string ReplaceMode = "replace";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICertificateService _certificates;
    private readonly ILogger<QuizService> _logger;

    public QuizService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICertificateService certificates,
        ILogger<QuizService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _certificates = certificates;
        _logger = logger;
    }

    public async Task<QuizDto> GetByResourceAsync(Guid resourceId, CancellationToken ct)
    {
        var quiz = await _db.Quizzes
            .AsNoTracking()
            .Include(q => q.Resource)
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.ResourceId == resourceId, ct)
            ?? throw new NotFoundException("Bu resurs üçün imtahan yaradılmayıb.");

        return ToDto(quiz);
    }

    public async Task<QuizDto> CreateOrUpdateAsync(Guid resourceId, CreateQuizRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var resource = await _db.Resources
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct)
            ?? throw NotFoundException.For("Resurs", resourceId);

        if (resource.AuthorId != userId && !_currentUser.IsAdmin)
        {
            throw new ForbiddenException("İmtahanı yalnız resursun müəllifi və ya admin idarə edə bilər.");
        }

        var quiz = await _db.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.ResourceId == resourceId, ct);

        if (quiz is null)
        {
            quiz = new Quiz
            {
                ResourceId = resourceId,
                PassPercent = request.PassPercent,
                CreatedAt = DateTime.UtcNow
            };
            _db.Quizzes.Add(quiz);
        }
        else
        {
            quiz.PassPercent = request.PassPercent;

            if (string.Equals(request.Mode, ReplaceMode, StringComparison.OrdinalIgnoreCase))
            {
                _db.QuizQuestions.RemoveRange(quiz.Questions);
                quiz.Questions.Clear();
            }
        }

        var nextIndex = quiz.Questions.Count == 0 ? 0 : quiz.Questions.Max(q => q.OrderIndex) + 1;

        foreach (var q in request.Questions)
        {
            ValidateQuestion(q);

            var question = new QuizQuestion
            {
                QuizId = quiz.Id,
                OrderIndex = nextIndex++,
                QuestionText = q.QuestionText.Trim(),
                Options = q.Options.Select(o => o.Trim()).ToList(),
                CorrectOptionIndex = q.CorrectOptionIndex
            };

            // Mövcud quiz-ə sual əlavə edəndə açıq Add lazımdır: Id əvvəlcədən doldurulduğu
            // üçün yalnız naviqasiya kolleksiyası EF-ə bunu "Modified" kimi göstərər.
            // quiz.Questions relationship fixup ilə avtomatik yenilənir.
            _db.QuizQuestions.Add(question);
        }

        await _db.SaveChangesAsync(ct);

        quiz.Resource = resource;
        return ToDto(quiz);
    }

    public async Task<IReadOnlyList<QuizQuestionDto>> GetQuestionsAsync(Guid quizId, CancellationToken ct)
    {
        _currentUser.RequireUserId();

        var questions = await _db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(ct);

        if (questions.Count == 0)
        {
            var quizExists = await _db.Quizzes.AnyAsync(q => q.Id == quizId, ct);
            if (!quizExists)
            {
                throw NotFoundException.For("İmtahan", quizId);
            }
        }

        // CorrectOptionIndex qəsdən DTO-ya köçürülmür - cavablar heç vaxt müştəriyə getmir.
        return questions
            .Select(q => new QuizQuestionDto(q.Id, q.OrderIndex, q.QuestionText, q.Options))
            .ToList();
    }

    public async Task<QuizResultDto> SubmitAsync(Guid quizId, SubmitQuizRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var quiz = await _db.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId, ct)
            ?? throw NotFoundException.For("İmtahan", quizId);

        if (quiz.Questions.Count == 0)
        {
            throw new BadRequestException("Bu imtahanda sual yoxdur.");
        }

        var answers = request.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Last().SelectedIndex);

        var correctCount = quiz.Questions.Count(q =>
            answers.TryGetValue(q.Id, out var selected) && selected == q.CorrectOptionIndex);

        var scorePercent = (int)Math.Round(
            correctCount * 100d / quiz.Questions.Count, MidpointRounding.AwayFromZero);

        var passed = scorePercent >= quiz.PassPercent;

        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            UserId = userId,
            ScorePercent = scorePercent,
            Passed = passed,
            AttemptedAt = DateTime.UtcNow
        };

        _db.QuizAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        string? certificateCode = null;
        if (passed)
        {
            var certificate = await _certificates.IssueForQuizAttemptAsync(attempt.Id, ct);
            certificateCode = certificate.Code;
        }

        _logger.LogInformation(
            "İmtahan cəhdi qeydə alındı. AttemptId: {AttemptId}, Nəticə: {Score}%, Keçdi: {Passed}",
            attempt.Id, scorePercent, passed);

        return new QuizResultDto(
            attempt.Id,
            scorePercent,
            correctCount,
            quiz.Questions.Count,
            quiz.PassPercent,
            passed,
            certificateCode);
    }

    private static void ValidateQuestion(CreateQuizQuestionRequest q)
    {
        if (string.IsNullOrWhiteSpace(q.QuestionText))
        {
            throw new ValidationFailedException("questions", "Sual mətni boş ola bilməz.");
        }

        if (q.Options.Count < 2)
        {
            throw new ValidationFailedException("questions", "Hər sualda ən azı 2 variant olmalıdır.");
        }

        if (q.CorrectOptionIndex < 0 || q.CorrectOptionIndex >= q.Options.Count)
        {
            throw new ValidationFailedException("questions", "Düzgün variantın indeksi variantlar sırasından kənardadır.");
        }
    }

    private static QuizDto ToDto(Quiz quiz) => new(
        quiz.Id,
        quiz.ResourceId,
        quiz.Resource?.Name ?? string.Empty,
        quiz.PassPercent,
        quiz.Questions.Count,
        quiz.CreatedAt);
}
