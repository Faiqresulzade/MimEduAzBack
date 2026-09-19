using MimeduAz.Contracts.Exams;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Mappings;

/// <summary>
/// Sınağın cari istifadəçiyə görə dəyişən görünüşü — resurslardakı
/// <see cref="ResourceViewContext"/> ilə eyni məntiq.
/// </summary>
public sealed record ExamViewContext(bool IsPurchased, bool IsAuthor, bool IsAdmin)
{
    public static readonly ExamViewContext Anonymous = new(false, false, false);

    public bool HasPaidAccess => IsPurchased || IsAuthor || IsAdmin;
}

public static class ExamMappings
{
    public static ExamDto ToDto(
        this Exam exam,
        ExamViewContext? view = null,
        int? attemptCount = null)
    {
        var context = view ?? ExamViewContext.Anonymous;

        return new ExamDto(
            exam.Id,
            exam.Name,
            exam.Subject,
            exam.Grade,
            exam.AuthorId,
            exam.Author?.FullName ?? string.Empty,
            exam.DurationMinutes,
            exam.PassPercent,
            exam.IsPaid,
            exam.Price,
            exam.Status,
            exam.Sections.Count,
            exam.Sections.Sum(s => s.Questions.Count),
            attemptCount ?? exam.Attempts.Count,
            context.IsPurchased,
            CanAccess(exam, context),
            exam.CreatedAt,
            exam.ApprovedAt);
    }

    public static ExamDetailDto ToDetailDto(
        this Exam exam,
        ExamViewContext? view = null,
        int? attemptCount = null,
        ExamAttemptSummaryDto? myAttempt = null)
    {
        var context = view ?? ExamViewContext.Anonymous;

        var sections = exam.Sections
            .OrderBy(s => s.OrderIndex)
            .Select(s => new ExamSectionSummaryDto(s.Id, s.OrderIndex, s.Subject, s.Questions.Count))
            .ToList();

        return new ExamDetailDto(
            exam.Id,
            exam.Name,
            exam.Description,
            exam.Subject,
            exam.Grade,
            exam.AuthorId,
            exam.Author?.FullName ?? string.Empty,
            exam.DurationMinutes,
            exam.PassPercent,
            exam.IsPaid,
            exam.Price,
            exam.Status,
            exam.RejectionReason,
            sections,
            sections.Sum(s => s.QuestionCount),
            attemptCount ?? exam.Attempts.Count,
            context.IsPurchased,
            CanAccess(exam, context),
            myAttempt,
            exam.CreatedAt,
            exam.ApprovedAt);
    }

    public static ExamAttemptSummaryDto ToSummaryDto(this ExamAttempt attempt, string? certificateCode) =>
        new(attempt.Id,
            attempt.ExamId,
            attempt.Exam?.Name ?? string.Empty,
            attempt.Status,
            attempt.ScorePercent,
            attempt.CorrectCount,
            attempt.QuestionCount,
            attempt.Passed,
            attempt.StartedAt,
            attempt.ExpiresAt,
            attempt.SubmittedAt,
            certificateCode);

    private static bool CanAccess(Exam exam, ExamViewContext context) =>
        !exam.IsPaid || context.HasPaidAccess;
}
