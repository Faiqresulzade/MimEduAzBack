using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Contracts.Trainings;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class TrainingService : ITrainingService
{
    private const string ReplaceMode = "replace";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICertificateService _certificates;
    private readonly ILogger<TrainingService> _logger;

    public TrainingService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICertificateService certificates,
        ILogger<TrainingService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _certificates = certificates;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TrainingDto>> GetAsync(CancellationToken ct)
    {
        var trainings = await _db.Trainings
            .AsNoTracking()
            .OrderBy(t => t.Format)
            .ThenBy(t => t.Name)
            .Select(t => new
            {
                Training = t,
                SeatsTaken = t.Enrollments.Count,
                LessonCount = t.Lessons.Count
            })
            .ToListAsync(ct);

        return trainings.Select(x => x.Training.ToDto(x.SeatsTaken, x.LessonCount)).ToList();
    }

    public async Task<TrainingDetailDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var training = await _db.Trainings
            .AsNoTracking()
            .Include(t => t.SyllabusItems)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw NotFoundException.For("Təlim", id);

        var seatsTaken = await _db.Enrollments.CountAsync(e => e.TrainingId == id, ct);
        var lessonCount = await _db.TrainingLessons.CountAsync(l => l.TrainingId == id, ct);

        var isEnrolled = _currentUser.UserId is { } userId
            && await _db.Enrollments.AnyAsync(e => e.TrainingId == id && e.UserId == userId, ct);

        return training.ToDetailDto(seatsTaken, lessonCount, isEnrolled);
    }

    public async Task<TrainingDetailDto> CreateAsync(CreateTrainingRequest request, CancellationToken ct)
    {
        var training = new Training
        {
            Name = request.Name.Trim(),
            Format = request.Format,
            Description = request.Description.Trim(),
            Price = request.Price,
            DurationHours = request.DurationHours,
            MetaLabel = request.MetaLabel.Trim(),
            // Yer limiti yalnız canlı təlimlərdə mənalıdır; onlayn/video limitsizdir.
            SeatLimit = request.Format == TrainingFormat.Live ? request.SeatLimit : null,
            CreatedAt = DateTime.UtcNow
        };

        var lessonIndex = 0;
        foreach (var lesson in request.Lessons)
        {
            training.Lessons.Add(new TrainingLesson
            {
                TrainingId = training.Id,
                OrderIndex = lessonIndex++,
                Title = lesson.Title.Trim(),
                Description = lesson.Description.Trim(),
                VideoUrl = string.IsNullOrWhiteSpace(lesson.VideoUrl) ? null : lesson.VideoUrl.Trim(),
                DurationMinutes = lesson.DurationMinutes
            });
        }

        // Proqram maddələri göndərilməyibsə dərs başlıqlarından qurulur -
        // admin eyni siyahını iki dəfə yazmasın deyə.
        var syllabusTexts = request.Syllabus.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (syllabusTexts.Count == 0)
        {
            syllabusTexts = request.Lessons
                .Where(l => !string.IsNullOrWhiteSpace(l.Title))
                .Select(l => l.Title)
                .ToList();
        }

        var order = 0;
        foreach (var text in syllabusTexts)
        {
            training.SyllabusItems.Add(new TrainingSyllabusItem
            {
                TrainingId = training.Id,
                OrderIndex = order++,
                Text = text.Trim()
            });
        }

        _db.Trainings.Add(training);
        await _db.SaveChangesAsync(ct);

        return training.ToDetailDto(seatsTaken: 0, training.Lessons.Count, isEnrolled: false);
    }

    public async Task<IReadOnlyList<MyTrainingDto>> GetMineAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var enrollments = await _db.Enrollments
            .AsNoTracking()
            .Include(e => e.Training)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new
            {
                Enrollment = e,
                CompletedLessons = e.CompletedLessons.Count,
                TotalLessons = e.Training!.Lessons.Count
            })
            .ToListAsync(ct);

        var trainingIds = enrollments.Select(e => e.Enrollment.TrainingId).ToList();

        // Təlim üzrə verilmiş sertifikatları bir sorğu ilə götürüb yaddaşda uyğunlaşdırırıq.
        var certificates = await _db.Certificates
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.TrainingId != null && trainingIds.Contains(c.TrainingId.Value))
            .ToListAsync(ct);

        var certificateByTraining = certificates
            .GroupBy(c => c.TrainingId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.IssuedAt).First().Code);

        return enrollments.Select(x =>
        {
            var e = x.Enrollment;
            return new MyTrainingDto(
                e.Id,
                e.TrainingId,
                e.Training?.Name ?? string.Empty,
                e.Training?.Format ?? TrainingFormat.Online,
                e.Training?.MetaLabel ?? string.Empty,
                e.Training?.DurationHours ?? 0,
                e.ProgressPercent,
                x.CompletedLessons,
                x.TotalLessons,
                e.Status,
                e.EnrolledAt,
                e.CompletedAt,
                certificateByTraining.GetValueOrDefault(e.TrainingId));
        }).ToList();
    }

    public async Task<TrainingLessonsDto> GetLessonsAsync(Guid trainingId, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var training = await _db.Trainings
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trainingId, ct)
            ?? throw NotFoundException.For("Təlim", trainingId);

        var enrollment = await _db.Enrollments
            .AsNoTracking()
            .Include(e => e.CompletedLessons)
            .FirstOrDefaultAsync(e => e.TrainingId == trainingId && e.UserId == userId, ct);

        // Video linkləri yalnız təlimi almış istifadəçiyə açılır.
        if (enrollment is null && !_currentUser.IsAdmin)
        {
            throw new ForbiddenException("Dərsləri görmək üçün əvvəlcə bu təlimə yazılmalısınız.");
        }

        var lessons = await _db.TrainingLessons
            .AsNoTracking()
            .Where(l => l.TrainingId == trainingId)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync(ct);

        var completedAtByLesson = enrollment?.CompletedLessons
            .ToDictionary(c => c.LessonId, c => c.CompletedAt) ?? new Dictionary<Guid, DateTime>();

        var lessonDtos = lessons.Select(l => new TrainingLessonDto(
            l.Id,
            l.OrderIndex,
            l.Title,
            l.Description,
            l.VideoUrl,
            l.DurationMinutes,
            completedAtByLesson.ContainsKey(l.Id),
            completedAtByLesson.TryGetValue(l.Id, out var at) ? at : null)).ToList();

        var certificateCode = await GetCertificateCodeAsync(userId, trainingId, ct);

        return new TrainingLessonsDto(
            training.Id,
            training.Name,
            lessonDtos,
            completedAtByLesson.Count,
            lessons.Count,
            enrollment?.ProgressPercent ?? 0,
            enrollment?.Status ?? EnrollmentStatus.InProgress,
            certificateCode);
    }

    public async Task<LessonProgressDto> SetLessonCompletionAsync(
        Guid trainingId, Guid lessonId, bool isCompleted, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var enrollment = await _db.Enrollments
            .Include(e => e.CompletedLessons)
            .FirstOrDefaultAsync(e => e.TrainingId == trainingId && e.UserId == userId, ct)
            ?? throw new ForbiddenException("Bu təlimə yazılmamısınız.");

        var lesson = await _db.TrainingLessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lessonId && l.TrainingId == trainingId, ct)
            ?? throw NotFoundException.For("Dərs", lessonId);

        var existing = enrollment.CompletedLessons.FirstOrDefault(c => c.LessonId == lesson.Id);

        if (isCompleted && existing is null)
        {
            // Açıq Add lazımdır: Id əvvəlcədən doldurulduğu üçün yalnız naviqasiya
            // kolleksiyası EF-ə bunu "Modified" kimi göstərər.
            _db.LessonCompletions.Add(new LessonCompletion
            {
                EnrollmentId = enrollment.Id,
                LessonId = lesson.Id,
                CompletedAt = DateTime.UtcNow
            });
        }
        else if (!isCompleted && existing is not null)
        {
            _db.LessonCompletions.Remove(existing);
            enrollment.CompletedLessons.Remove(existing);
        }

        var totalLessons = await _db.TrainingLessons.CountAsync(l => l.TrainingId == trainingId, ct);

        // Add çağırışı relationship fixup ilə CompletedLessons-u artıq yeniləyir,
        // silmə isə yuxarıda əl ilə edilir - ona görə sayı birbaşa kolleksiyadan götürürük.
        // Əl ilə +1 etsək eyni dərs iki dəfə sayılar.
        var completedCount = Math.Clamp(enrollment.CompletedLessons.Count, 0, totalLessons);

        enrollment.ProgressPercent = totalLessons == 0
            ? 0
            : (int)Math.Round(completedCount * 100d / totalLessons, MidpointRounding.AwayFromZero);

        var justCompleted = totalLessons > 0
                            && completedCount == totalLessons
                            && enrollment.Status != EnrollmentStatus.Completed;

        if (justCompleted)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.CompletedAt = DateTime.UtcNow;
        }
        else if (completedCount < totalLessons)
        {
            enrollment.Status = EnrollmentStatus.InProgress;
            enrollment.CompletedAt = null;
        }

        await _db.SaveChangesAsync(ct);

        string? certificateCode = null;
        if (enrollment.Status == EnrollmentStatus.Completed)
        {
            // İdempotentdir: təkrar çağırışda mövcud sertifikat qaytarılır.
            var certificate = await _certificates.IssueForTrainingCompletionAsync(enrollment.Id, ct);
            certificateCode = certificate.Code;

            if (justCompleted)
            {
                _logger.LogInformation(
                    "Təlim tamamlandı. EnrollmentId: {EnrollmentId}, TrainingId: {TrainingId}",
                    enrollment.Id, trainingId);
            }
        }

        return new LessonProgressDto(
            trainingId,
            lessonId,
            isCompleted,
            completedCount,
            totalLessons,
            enrollment.ProgressPercent,
            enrollment.Status,
            certificateCode);
    }

    public async Task<TrainingLessonsDto> SaveLessonsAsync(
        Guid trainingId, SaveTrainingLessonsRequest request, CancellationToken ct)
    {
        var training = await _db.Trainings
            .Include(t => t.Lessons)
            .FirstOrDefaultAsync(t => t.Id == trainingId, ct)
            ?? throw NotFoundException.For("Təlim", trainingId);

        if (string.Equals(request.Mode, ReplaceMode, StringComparison.OrdinalIgnoreCase))
        {
            _db.TrainingLessons.RemoveRange(training.Lessons);
            training.Lessons.Clear();
        }

        var nextIndex = training.Lessons.Count == 0 ? 0 : training.Lessons.Max(l => l.OrderIndex) + 1;

        foreach (var lesson in request.Lessons)
        {
            _db.TrainingLessons.Add(new TrainingLesson
            {
                TrainingId = training.Id,
                OrderIndex = nextIndex++,
                Title = lesson.Title.Trim(),
                Description = lesson.Description.Trim(),
                VideoUrl = string.IsNullOrWhiteSpace(lesson.VideoUrl) ? null : lesson.VideoUrl.Trim(),
                DurationMinutes = lesson.DurationMinutes
            });
        }

        await _db.SaveChangesAsync(ct);

        // Dərs sayı dəyişdi - mövcud enrollment-lərin faizi yenidən hesablanmalıdır.
        await RecalculateProgressAsync(trainingId, ct);

        return await GetLessonsAsync(trainingId, ct);
    }

    /// <summary>
    /// Dərs sayı dəyişəndə bütün yazılmış istifadəçilərin faizini yenidən hesablayır ki,
    /// köhnə faizlər (məs. 100%) yeni dərslərdən sonra səhv qalmasın.
    /// </summary>
    private async Task RecalculateProgressAsync(Guid trainingId, CancellationToken ct)
    {
        var totalLessons = await _db.TrainingLessons.CountAsync(l => l.TrainingId == trainingId, ct);

        var enrollments = await _db.Enrollments
            .Include(e => e.CompletedLessons)
            .Where(e => e.TrainingId == trainingId)
            .ToListAsync(ct);

        foreach (var enrollment in enrollments)
        {
            var completed = enrollment.CompletedLessons.Count;

            enrollment.ProgressPercent = totalLessons == 0
                ? 0
                : (int)Math.Round(completed * 100d / totalLessons, MidpointRounding.AwayFromZero);

            if (totalLessons > 0 && completed >= totalLessons)
            {
                enrollment.Status = EnrollmentStatus.Completed;
                enrollment.CompletedAt ??= DateTime.UtcNow;
            }
            else
            {
                enrollment.Status = EnrollmentStatus.InProgress;
                enrollment.CompletedAt = null;
            }
        }

        if (enrollments.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<string?> GetCertificateCodeAsync(Guid userId, Guid trainingId, CancellationToken ct) =>
        await _db.Certificates
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.TrainingId == trainingId)
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(ct);
}
