using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Exams;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class ExamService : IExamService
{
    /// <summary>
    /// Şəbəkə gecikməsi üçün güzəşt. Sayğac sıfıra çatanda frontend avtomatik
    /// göndərir; paket bir neçə saniyə gec gəlsə cavab itməməlidir.
    /// </summary>
    private static readonly TimeSpan SubmitGrace = TimeSpan.FromSeconds(30);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;
    private readonly ICertificateService _certificates;
    private readonly INotificationService _notifications;
    private readonly FileStorageOptions _storage;
    private readonly ILogger<ExamService> _logger;

    public ExamService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files,
        ICertificateService certificates,
        INotificationService notifications,
        IOptions<FileStorageOptions> storage,
        ILogger<ExamService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
        _certificates = certificates;
        _notifications = notifications;
        _storage = storage.Value;
        _logger = logger;
    }

    // ------------------------------------------------------------ Kataloq

    public async Task<PagedResult<ExamDto>> GetAsync(ExamQuery query, CancellationToken ct)
    {
        var status = _currentUser.IsAdmin ? query.Status ?? ExamStatus.Approved : ExamStatus.Approved;

        var q = _db.Exams
            .AsNoTracking()
            .Include(e => e.Author)
            .Include(e => e.Sections)
                .ThenInclude(s => s.Questions)
            .Where(e => e.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Subject))
        {
            var subject = query.Subject.Trim();
            q = q.Where(e => e.Subject == subject);
        }

        if (query.Grade is > 0)
        {
            q = q.Where(e => e.Grade == query.Grade);
        }

        if (query.IsPaid is not null)
        {
            q = q.Where(e => e.IsPaid == query.IsPaid);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(e => e.Name.ToLower().Contains(term));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ExamDto>
        {
            Items = await MapManyAsync(items, ct),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<ExamDetailDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var exam = await LoadWithContentAsync(id, tracking: false, ct);

        // Təsdiqlənməmiş sınağı yalnız müəllifi və admin görür.
        if (exam.Status != ExamStatus.Approved
            && !_currentUser.IsAdmin
            && exam.AuthorId != _currentUser.UserId)
        {
            throw NotFoundException.For("Sınaq", id);
        }

        var view = await BuildViewAsync(exam, ct);
        var attemptCount = await CountAttemptsAsync(id, ct);
        var myAttempt = await LoadMyAttemptSummaryAsync(id, ct);

        return exam.ToDetailDto(view, attemptCount, myAttempt);
    }

    public async Task<IReadOnlyList<ExamDto>> GetMineAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var exams = await _db.Exams
            .AsNoTracking()
            .Include(e => e.Author)
            .Include(e => e.Sections)
                .ThenInclude(s => s.Questions)
            .Where(e => e.AuthorId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        return await MapManyAsync(exams, ct);
    }

    public async Task<IReadOnlyList<ExamDto>> GetPurchasedAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var purchasedIds = await _db.OrderItems
            .AsNoTracking()
            .Where(oi =>
                oi.ItemType == CatalogItemType.Exam &&
                oi.Order!.UserId == userId &&
                oi.Order.Status == OrderStatus.Paid)
            .Select(oi => oi.ItemId)
            .Distinct()
            .ToListAsync(ct);

        if (purchasedIds.Count == 0)
        {
            return Array.Empty<ExamDto>();
        }

        var exams = await _db.Exams
            .AsNoTracking()
            .Include(e => e.Author)
            .Include(e => e.Sections)
                .ThenInclude(s => s.Questions)
            .Where(e => purchasedIds.Contains(e.Id))
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        var purchased = purchasedIds.ToHashSet();
        var attemptCounts = await CountAttemptsAsync(exams.Select(e => e.Id).ToList(), ct);

        return exams
            .Select(e => e.ToDto(BuildView(e, purchased), attemptCounts.GetValueOrDefault(e.Id)))
            .ToList();
    }

    // ------------------------------------------------------------ Yaratma / redaktə

    public async Task<ExamDetailDto> CreateAsync(CreateExamRequest request, CancellationToken ct)
    {
        var authorId = _currentUser.RequireUserId();

        ValidateSections(request.Sections);

        var exam = new Exam
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Subject = request.Subject.Trim(),
            Grade = request.Grade,
            AuthorId = authorId,
            DurationMinutes = request.DurationMinutes,
            PassPercent = request.PassPercent,
            IsPaid = request.IsPaid,
            Price = request.IsPaid ? request.Price : 0m,
            Status = ExamStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Exams.Add(exam);
        BuildSections(exam, request.Sections);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Yeni sınaq yaradıldı və moderasiyaya göndərildi. ExamId: {ExamId}, Sual sayı: {Count}",
            exam.Id, exam.Sections.Sum(s => s.Questions.Count));

        exam.Author = await _db.Users.FirstOrDefaultAsync(u => u.Id == authorId, ct);
        await _notifications.NotifyAdminsOfPendingExamAsync(exam, ct);

        return exam.ToDetailDto(AuthorView, attemptCount: 0);
    }

    public async Task<ExamDetailDto> UpdateAsync(Guid id, UpdateExamRequest request, CancellationToken ct)
    {
        var exam = await LoadWithContentAsync(id, tracking: true, ct);
        RequireOwnership(exam);

        exam.Name = request.Name.Trim();
        exam.Description = request.Description.Trim();
        exam.Subject = request.Subject.Trim();
        exam.Grade = request.Grade;
        exam.DurationMinutes = request.DurationMinutes;
        exam.PassPercent = request.PassPercent;
        exam.IsPaid = request.IsPaid;
        exam.Price = request.IsPaid ? request.Price : 0m;

        // Rədd edilmiş sınaq düzəldilirsə yenidən növbəyə düşür.
        if (exam.Status == ExamStatus.Rejected)
        {
            exam.Status = ExamStatus.Pending;
            exam.RejectionReason = null;
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyAdminsOfPendingExamAsync(exam, ct);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Sınaq yeniləndi. ExamId: {ExamId}", id);

        return exam.ToDetailDto(
            await BuildViewAsync(exam, ct),
            await CountAttemptsAsync(id, ct));
    }

    public async Task<ExamDetailDto> SaveSectionsAsync(
        Guid id, SaveExamSectionsRequest request, CancellationToken ct)
    {
        var exam = await LoadWithContentAsync(id, tracking: true, ct);
        RequireOwnership(exam);
        ValidateSections(request.Sections);

        var attemptCount = await CountAttemptsAsync(id, ct);

        // Sualları dəyişmək keçmiş cəhdlərin balını mənasız edərdi.
        if (attemptCount > 0)
        {
            throw new ConflictException(
                $"Bu sınağı {attemptCount} nəfər verib, sualları dəyişmək olmaz. Yeni sınaq yaradın.");
        }

        var oldSections = exam.Sections.ToList();
        _db.ExamSections.RemoveRange(oldSections);

        foreach (var section in oldSections)
        {
            exam.Sections.Remove(section);
        }

        BuildSections(exam, request.Sections);

        // Məzmun dəyişdiyi üçün təsdiqlənmiş sınaq yenidən moderasiyadan keçməlidir.
        var needsModeration = exam.Status != ExamStatus.Pending;
        if (needsModeration)
        {
            exam.Status = ExamStatus.Pending;
            exam.RejectionReason = null;
            exam.ApprovedAt = null;
        }

        await _db.SaveChangesAsync(ct);

        if (needsModeration)
        {
            await _notifications.NotifyAdminsOfPendingExamAsync(exam, ct);
        }

        _logger.LogInformation(
            "Sınağın sualları əvəz edildi. ExamId: {ExamId}, Yeni sual sayı: {Count}",
            id, exam.Sections.Sum(s => s.Questions.Count));

        return exam.ToDetailDto(await BuildViewAsync(exam, ct), attemptCount: 0);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var exam = await LoadWithContentAsync(id, tracking: true, ct);
        RequireOwnership(exam);

        var attemptCount = await CountAttemptsAsync(id, ct);

        if (attemptCount > 0)
        {
            throw new ConflictException(
                $"Bu sınağı {attemptCount} nəfər verib, silinə bilməz. Əvəzinə məlumatlarını redaktə edin.");
        }

        // Silinən sınaq kiminsə səbətində qalarsa ödəniş mərhələsində 404 verərdi.
        var staleCartItems = await _db.CartItems
            .Where(i => i.ItemType == CatalogItemType.Exam && i.ItemId == id)
            .ToListAsync(ct);

        if (staleCartItems.Count > 0)
        {
            _db.CartItems.RemoveRange(staleCartItems);
        }

        // Sual şəkilləri bazadan sonra qalmasın deyə diskdən də silinir.
        var imagePaths = exam.Sections
            .SelectMany(s => s.Questions)
            .Select(q => q.ImagePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync(ct);

        foreach (var path in imagePaths)
        {
            try
            {
                await _files.DeleteAsync(path!, ct);
            }
            catch (Exception ex)
            {
                // Şəklin qalması silmə əməliyyatını pozmamalıdır.
                _logger.LogWarning(ex, "Sual şəkli silinə bilmədi: {Path}", path);
            }
        }

        _logger.LogInformation("Sınaq silindi. ExamId: {ExamId}", id);
    }

    public async Task<ExamImageDto> UploadQuestionImageAsync(
        Guid examId, ExamImageUpload image, CancellationToken ct)
    {
        var exam = await _db.Exams
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == examId, ct)
            ?? throw NotFoundException.For("Sınaq", examId);

        RequireOwnership(exam);
        ValidateImage(image);

        var storedPath = await _files.SaveAsync(
            image.Content, image.FileName, ct, _storage.ImageRootPath);

        _logger.LogInformation("Sual şəkli yükləndi. ExamId: {ExamId}, Yol: {Path}", examId, storedPath);

        return new ExamImageDto(storedPath, _files.GetPublicUrl(storedPath));
    }

    // ------------------------------------------------------------ Sınaq gedişi

    public async Task<ExamRunDto> StartAsync(Guid examId, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var exam = await LoadWithContentAsync(examId, tracking: false, ct);

        if (exam.Status != ExamStatus.Approved)
        {
            throw new BadRequestException("Bu sınaq hələ təsdiqlənməyib.");
        }

        if (exam.Sections.Sum(s => s.Questions.Count) == 0)
        {
            throw new BadRequestException("Bu sınaqda sual yoxdur.");
        }

        if (exam.AuthorId == userId)
        {
            throw new BadRequestException("Öz sınağınızı verə bilməzsiniz.");
        }

        await RequireAccessAsync(exam, userId, ct);

        var existing = await _db.ExamAttempts
            .FirstOrDefaultAsync(a => a.ExamId == examId && a.UserId == userId, ct);

        if (existing is not null)
        {
            // Təhvil verilmiş cəhd təkrar açılmır - sınaq bir dəfə verilir.
            if (existing.Status != ExamAttemptStatus.InProgress)
            {
                throw new ConflictException("Bu sınağı artıq vermisiniz, nəticəyə baxa bilərsiniz.");
            }

            // Vaxtı bitmiş açıq cəhd bir daha davam etdirilmir.
            if (DateTime.UtcNow > existing.ExpiresAt + SubmitGrace)
            {
                existing.Status = ExamAttemptStatus.Expired;
                existing.SubmittedAt = existing.ExpiresAt;
                existing.QuestionCount = exam.Sections.Sum(s => s.Questions.Count);
                await _db.SaveChangesAsync(ct);

                throw new ConflictException("Bu sınağın vaxtı bitib.");
            }

            // Səhifə yenilənibsə eyni cəhd davam edir - vaxt sıfırlanmır.
            return BuildRun(exam, existing);
        }

        var startedAt = DateTime.UtcNow;
        var attempt = new ExamAttempt
        {
            ExamId = examId,
            UserId = userId,
            StartedAt = startedAt,
            ExpiresAt = startedAt.AddMinutes(exam.DurationMinutes),
            Status = ExamAttemptStatus.InProgress,
            QuestionCount = exam.Sections.Sum(s => s.Questions.Count)
        };

        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sınaq başladıldı. ExamId: {ExamId}, AttemptId: {AttemptId}, Bitmə vaxtı: {ExpiresAt}",
            examId, attempt.Id, attempt.ExpiresAt);

        return BuildRun(exam, attempt);
    }

    public async Task<ExamResultDto> SubmitAsync(
        Guid attemptId, SubmitExamRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var attempt = await _db.ExamAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct)
            ?? throw NotFoundException.For("Sınaq cəhdi", attemptId);

        if (attempt.UserId != userId)
        {
            throw new ForbiddenException("Bu sınaq cəhdi sizə aid deyil.");
        }

        if (attempt.Status != ExamAttemptStatus.InProgress)
        {
            throw new ConflictException("Bu cəhd artıq bağlanıb.");
        }

        var exam = await LoadWithContentAsync(attempt.ExamId, tracking: false, ct);
        var questions = exam.Sections.SelectMany(s => s.Questions).ToList();

        // Vaxt bitibsə nəticə qiymətləndirilmir - sertifikat da verilmir.
        if (DateTime.UtcNow > attempt.ExpiresAt + SubmitGrace)
        {
            attempt.Status = ExamAttemptStatus.Expired;
            attempt.SubmittedAt = DateTime.UtcNow;
            attempt.QuestionCount = questions.Count;
            attempt.ScorePercent = 0;
            attempt.CorrectCount = 0;
            attempt.Passed = false;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Sınaq vaxtı bitdiyi üçün qiymətləndirilmədi. AttemptId: {AttemptId}", attemptId);

            throw new ConflictException("Sınağın vaxtı bitib, cavablar qəbul olunmadı.");
        }

        var selections = request.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Last().SelectedIndex);

        var questionIds = questions.Select(q => q.Id).ToHashSet();
        var correctCount = 0;

        foreach (var question in questions)
        {
            var selected = selections.GetValueOrDefault(question.Id);
            var isCorrect = selected is not null && selected == question.CorrectOptionIndex;

            if (isCorrect)
            {
                correctCount++;
            }

            // Id əvvəlcədən dolu olduğu üçün açıq şəkildə DbSet-ə əlavə edilir;
            // yalnız kolleksiyaya atsaq EF onu "Modified" kimi izləyər.
            _db.ExamAnswers.Add(new ExamAnswer
            {
                AttemptId = attempt.Id,
                QuestionId = question.Id,
                SelectedIndex = selected,
                IsCorrect = isCorrect
            });
        }

        // Başqa sınağın sualına cavab göndərilibsə səssizcə nəzərə alınmır.
        var foreign = selections.Keys.Where(id => !questionIds.Contains(id)).ToList();
        if (foreign.Count > 0)
        {
            _logger.LogWarning(
                "Sınaqda olmayan {Count} sual üçün cavab göndərildi, nəzərə alınmadı. AttemptId: {AttemptId}",
                foreign.Count, attemptId);
        }

        var scorePercent = questions.Count == 0
            ? 0
            : (int)Math.Round(correctCount * 100d / questions.Count, MidpointRounding.AwayFromZero);

        attempt.Status = ExamAttemptStatus.Submitted;
        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.QuestionCount = questions.Count;
        attempt.CorrectCount = correctCount;
        attempt.ScorePercent = scorePercent;
        attempt.Passed = scorePercent >= exam.PassPercent;

        await _db.SaveChangesAsync(ct);

        string? certificateCode = null;
        if (attempt.Passed)
        {
            var certificate = await _certificates.IssueForExamAttemptAsync(attempt.Id, ct);
            certificateCode = certificate.Code;
        }

        _logger.LogInformation(
            "Sınaq təhvil verildi. AttemptId: {AttemptId}, Nəticə: {Score}%, Keçdi: {Passed}",
            attemptId, scorePercent, attempt.Passed);

        return BuildResult(exam, attempt, certificateCode);
    }

    public async Task<ExamResultDto> GetResultAsync(Guid attemptId, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var attempt = await _db.ExamAttempts
            .AsNoTracking()
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct)
            ?? throw NotFoundException.For("Sınaq cəhdi", attemptId);

        var exam = await LoadWithContentAsync(attempt.ExamId, tracking: false, ct);

        // Nəticəni cəhdin sahibi, sınağın müəllifi və admin görə bilər.
        if (attempt.UserId != userId && exam.AuthorId != userId && !_currentUser.IsAdmin)
        {
            throw new ForbiddenException("Bu sınaq nəticəsi sizə aid deyil.");
        }

        if (attempt.Status == ExamAttemptStatus.InProgress)
        {
            throw new BadRequestException("Sınaq hələ təhvil verilməyib.");
        }

        var certificateCode = await _db.Certificates
            .AsNoTracking()
            .Where(c => c.ExamAttemptId == attemptId)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(ct);

        return BuildResult(exam, attempt, certificateCode);
    }

    public async Task<IReadOnlyList<ExamAttemptSummaryDto>> GetMyAttemptsAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var attempts = await _db.ExamAttempts
            .AsNoTracking()
            .Include(a => a.Exam)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

        if (attempts.Count == 0)
        {
            return Array.Empty<ExamAttemptSummaryDto>();
        }

        var attemptIds = attempts.Select(a => a.Id).ToList();

        var codes = await _db.Certificates
            .AsNoTracking()
            .Where(c => c.ExamAttemptId != null && attemptIds.Contains(c.ExamAttemptId!.Value))
            .ToDictionaryAsync(c => c.ExamAttemptId!.Value, c => c.Code, ct);

        return attempts
            .Select(a => a.ToSummaryDto(codes.GetValueOrDefault(a.Id)))
            .ToList();
    }

    // ------------------------------------------------------------ Köməkçilər

    private ExamViewContext AuthorView => new(IsPurchased: false, IsAuthor: true, _currentUser.IsAdmin);

    private async Task<Exam> LoadWithContentAsync(Guid id, bool tracking, CancellationToken ct)
    {
        var q = _db.Exams
            .Include(e => e.Author)
            .Include(e => e.Sections)
                .ThenInclude(s => s.Questions)
            .AsQueryable();

        if (!tracking)
        {
            q = q.AsNoTracking();
        }

        return await q.FirstOrDefaultAsync(e => e.Id == id, ct)
               ?? throw NotFoundException.For("Sınaq", id);
    }

    private void RequireOwnership(Exam exam)
    {
        if (exam.AuthorId != _currentUser.UserId && !_currentUser.IsAdmin)
        {
            throw new ForbiddenException("Bu sınağı yalnız müəllifi və ya admin idarə edə bilər.");
        }
    }

    /// <summary>Ödənişli sınağa çıxışı olmayan iştirakçını dayandırır.</summary>
    private async Task RequireAccessAsync(Exam exam, Guid userId, CancellationToken ct)
    {
        if (!exam.IsPaid || _currentUser.IsAdmin)
        {
            return;
        }

        var purchased = await _db.OrderItems
            .AnyAsync(oi =>
                oi.ItemType == CatalogItemType.Exam &&
                oi.ItemId == exam.Id &&
                oi.Order!.UserId == userId &&
                oi.Order.Status == OrderStatus.Paid, ct);

        if (!purchased)
        {
            throw new ForbiddenException("Bu sınağa başlamaq üçün əvvəlcə satın almalısınız.");
        }
    }

    private async Task<HashSet<Guid>> GetPurchasedIdsAsync(
        IReadOnlyCollection<Guid> examIds, CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        if (userId is null || examIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = await _db.OrderItems
            .AsNoTracking()
            .Where(oi =>
                oi.ItemType == CatalogItemType.Exam &&
                examIds.Contains(oi.ItemId) &&
                oi.Order!.UserId == userId &&
                oi.Order.Status == OrderStatus.Paid)
            .Select(oi => oi.ItemId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    private ExamViewContext BuildView(Exam exam, IReadOnlySet<Guid> purchasedIds) =>
        new(purchasedIds.Contains(exam.Id),
            _currentUser.UserId is not null && exam.AuthorId == _currentUser.UserId,
            _currentUser.IsAdmin);

    private async Task<ExamViewContext> BuildViewAsync(Exam exam, CancellationToken ct)
    {
        if (!exam.IsPaid)
        {
            return BuildView(exam, new HashSet<Guid>());
        }

        var purchased = await GetPurchasedIdsAsync(new[] { exam.Id }, ct);
        return BuildView(exam, purchased);
    }

    private async Task<List<ExamDto>> MapManyAsync(IReadOnlyList<Exam> exams, CancellationToken ct)
    {
        var paidIds = exams.Where(e => e.IsPaid).Select(e => e.Id).ToList();
        var purchased = await GetPurchasedIdsAsync(paidIds, ct);
        var attemptCounts = await CountAttemptsAsync(exams.Select(e => e.Id).ToList(), ct);

        return exams
            .Select(e => e.ToDto(BuildView(e, purchased), attemptCounts.GetValueOrDefault(e.Id)))
            .ToList();
    }

    private Task<int> CountAttemptsAsync(Guid examId, CancellationToken ct) =>
        _db.ExamAttempts.CountAsync(a => a.ExamId == examId, ct);

    private async Task<Dictionary<Guid, int>> CountAttemptsAsync(
        IReadOnlyCollection<Guid> examIds, CancellationToken ct)
    {
        if (examIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // Səhifədəki bütün sınaqlar üçün tək sorğu - N+1 olmasın deyə.
        return await _db.ExamAttempts
            .AsNoTracking()
            .Where(a => examIds.Contains(a.ExamId))
            .GroupBy(a => a.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExamId, x => x.Count, ct);
    }

    private async Task<ExamAttemptSummaryDto?> LoadMyAttemptSummaryAsync(Guid examId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return null;
        }

        var attempt = await _db.ExamAttempts
            .AsNoTracking()
            .Include(a => a.Exam)
            .FirstOrDefaultAsync(a => a.ExamId == examId && a.UserId == userId, ct);

        if (attempt is null)
        {
            return null;
        }

        var code = await _db.Certificates
            .AsNoTracking()
            .Where(c => c.ExamAttemptId == attempt.Id)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(ct);

        return attempt.ToSummaryDto(code);
    }

    private ExamRunDto BuildRun(Exam exam, ExamAttempt attempt)
    {
        var sections = exam.Sections
            .OrderBy(s => s.OrderIndex)
            .Select(s => new ExamRunSectionDto(
                s.Id,
                s.OrderIndex,
                s.Subject,
                s.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new ExamRunQuestionDto(
                        q.Id,
                        q.OrderIndex,
                        q.QuestionText,
                        q.ImagePath is null ? null : _files.GetPublicUrl(q.ImagePath),
                        q.Options))
                    .ToList()))
            .ToList();

        var remaining = (int)Math.Max(0, (attempt.ExpiresAt - DateTime.UtcNow).TotalSeconds);

        return new ExamRunDto(
            attempt.Id,
            exam.Id,
            exam.Name,
            exam.DurationMinutes,
            attempt.StartedAt,
            attempt.ExpiresAt,
            remaining,
            sections,
            sections.Sum(s => s.Questions.Count));
    }

    private ExamResultDto BuildResult(Exam exam, ExamAttempt attempt, string? certificateCode)
    {
        var answers = attempt.Answers.ToDictionary(a => a.QuestionId);

        var sections = exam.Sections
            .OrderBy(s => s.OrderIndex)
            .Select(s =>
            {
                var questions = s.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q =>
                    {
                        var answer = answers.GetValueOrDefault(q.Id);
                        return new ExamAnswerReviewDto(
                            q.Id,
                            q.OrderIndex,
                            q.QuestionText,
                            q.ImagePath is null ? null : _files.GetPublicUrl(q.ImagePath),
                            q.Options,
                            q.CorrectOptionIndex,
                            answer?.SelectedIndex,
                            answer?.IsCorrect ?? false);
                    })
                    .ToList();

                var correct = questions.Count(q => q.IsCorrect);

                return new ExamSectionResultDto(
                    s.Id,
                    s.Subject,
                    correct,
                    questions.Count,
                    questions.Count == 0
                        ? 0
                        : (int)Math.Round(correct * 100d / questions.Count, MidpointRounding.AwayFromZero),
                    questions);
            })
            .ToList();

        var finishedAt = attempt.SubmittedAt ?? attempt.ExpiresAt;

        return new ExamResultDto(
            attempt.Id,
            exam.Id,
            exam.Name,
            attempt.Status,
            attempt.ScorePercent,
            attempt.CorrectCount,
            attempt.QuestionCount,
            exam.PassPercent,
            attempt.Passed,
            attempt.StartedAt,
            attempt.SubmittedAt,
            (int)Math.Max(0, (finishedAt - attempt.StartedAt).TotalSeconds),
            sections,
            certificateCode);
    }

    private void BuildSections(Exam exam, List<CreateExamSectionRequest> requests)
    {
        var sectionIndex = 0;

        foreach (var sectionRequest in requests)
        {
            var section = new ExamSection
            {
                ExamId = exam.Id,
                OrderIndex = sectionIndex++,
                Subject = sectionRequest.Subject.Trim()
            };

            // Id əvvəlcədən dolu olduğu üçün açıq şəkildə DbSet-ə əlavə edilir. Yalnız
            // naviqasiya kolleksiyasına atsaq, artıq izlənən sınaqda EF bunları "Modified"
            // kimi görər və UPDATE 0 sətir toxunduğu üçün concurrency xətası atar.
            // exam.Sections relationship fixup ilə avtomatik dolur.
            _db.ExamSections.Add(section);

            var questionIndex = 0;
            foreach (var questionRequest in sectionRequest.Questions)
            {
                _db.ExamQuestions.Add(new ExamQuestion
                {
                    SectionId = section.Id,
                    OrderIndex = questionIndex++,
                    QuestionText = questionRequest.QuestionText.Trim(),
                    ImagePath = string.IsNullOrWhiteSpace(questionRequest.ImagePath)
                        ? null
                        : questionRequest.ImagePath.Trim(),
                    Options = questionRequest.Options.Select(o => o.Trim()).ToList(),
                    CorrectOptionIndex = questionRequest.CorrectOptionIndex
                });
            }
        }
    }

    private static void ValidateSections(List<CreateExamSectionRequest> sections)
    {
        if (sections.Count == 0)
        {
            throw new ValidationFailedException("sections", "Sınaqda ən azı bir fənn bölməsi olmalıdır.");
        }

        foreach (var section in sections)
        {
            if (section.Questions.Count == 0)
            {
                throw new ValidationFailedException(
                    "sections", $"«{section.Subject}» bölməsində ən azı bir sual olmalıdır.");
            }

            foreach (var question in section.Questions)
            {
                // Sual ya mətnlə, ya şəkillə ifadə olunmalıdır - ikisi də boş ola bilməz.
                if (string.IsNullOrWhiteSpace(question.QuestionText)
                    && string.IsNullOrWhiteSpace(question.ImagePath))
                {
                    throw new ValidationFailedException(
                        "questionText", "Sual ya mətn, ya da şəkil ehtiva etməlidir.");
                }

                if (question.Options.Count < 2)
                {
                    throw new ValidationFailedException(
                        "options", "Hər sualda ən azı iki variant olmalıdır.");
                }

                if (question.CorrectOptionIndex < 0 || question.CorrectOptionIndex >= question.Options.Count)
                {
                    throw new ValidationFailedException(
                        "correctOptionIndex", "Düzgün variantın indeksi variant siyahısından kənardadır.");
                }
            }
        }
    }

    private void ValidateImage(ExamImageUpload image)
    {
        if (image.Length <= 0)
        {
            throw new ValidationFailedException("file", "Şəkil boşdur.");
        }

        if (image.Length > _storage.MaxImageSizeBytes)
        {
            var limitMb = _storage.MaxImageSizeBytes / (1024 * 1024);
            throw new ValidationFailedException("file", $"Şəklin ölçüsü {limitMb} MB-dan çox ola bilməz.");
        }

        var extension = Path.GetExtension(image.FileName);
        if (string.IsNullOrWhiteSpace(extension)
            || !_storage.AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            var allowed = string.Join(", ", _storage.AllowedImageExtensions);
            throw new ValidationFailedException("file", $"Yalnız bu formatlar qəbul olunur: {allowed}.");
        }
    }
}
