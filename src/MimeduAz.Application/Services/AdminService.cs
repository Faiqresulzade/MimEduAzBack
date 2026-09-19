using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Admin;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Exams;
using MimeduAz.Contracts.Orders;
using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly CommissionOptions _commission;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IApplicationDbContext db,
        IOptions<CommissionOptions> commission,
        ILogger<AdminService> logger)
    {
        _db = db;
        _commission = commission.Value;
        _logger = logger;
    }

    /// <summary>
    /// Moderator resursu təsdiqləməzdən əvvəl onun məzmununu görməlidir,
    /// ona görə admin cavablarında ödənişli link də açıq gedir.
    /// </summary>
    private static readonly ResourceViewContext ModeratorView =
        new(IsPurchased: false, IsAuthor: false, IsAdmin: true);

    public async Task<IReadOnlyList<ResourceDetailDto>> GetPendingResourcesAsync(CancellationToken ct)
    {
        var resources = await _db.Resources
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .Where(r => r.Status == ResourceStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return resources.Select(r => r.ToDetailDto(ModeratorView)).ToList();
    }

    public async Task<ResourceDetailDto> ApproveResourceAsync(Guid resourceId, CancellationToken ct)
    {
        var resource = await LoadForModerationAsync(resourceId, ct);

        resource.Status = ResourceStatus.Approved;
        resource.ApprovedAt = DateTime.UtcNow;
        resource.RejectionReason = null;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Resurs təsdiqləndi. ResourceId: {ResourceId}", resourceId);

        return resource.ToDetailDto(ModeratorView);
    }

    public async Task<ResourceDetailDto> RejectResourceAsync(
        Guid resourceId, RejectResourceRequest request, CancellationToken ct)
    {
        var resource = await LoadForModerationAsync(resourceId, ct);

        resource.Status = ResourceStatus.Rejected;
        resource.ApprovedAt = null;
        resource.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Resurs rədd edildi. ResourceId: {ResourceId}", resourceId);

        return resource.ToDetailDto(ModeratorView);
    }

    /// <summary>Moderator sınağın bütün məzmununu görməlidir, ona görə admin görünüşü ilə qaytarılır.</summary>
    private static readonly ExamViewContext ExamModeratorView =
        new(IsPurchased: false, IsAuthor: false, IsAdmin: true);

    public async Task<IReadOnlyList<ExamDetailDto>> GetPendingExamsAsync(CancellationToken ct)
    {
        var exams = await _db.Exams
            .AsNoTracking()
            .Include(e => e.Author)
            .Include(e => e.Sections)
                .ThenInclude(s => s.Questions)
            .Where(e => e.Status == ExamStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        return exams.Select(e => e.ToDetailDto(ExamModeratorView, attemptCount: 0)).ToList();
    }

    public async Task<ExamDetailDto> ApproveExamAsync(Guid examId, CancellationToken ct)
    {
        var exam = await LoadExamForModerationAsync(examId, ct);

        exam.Status = ExamStatus.Approved;
        exam.ApprovedAt = DateTime.UtcNow;
        exam.RejectionReason = null;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Sınaq təsdiqləndi. ExamId: {ExamId}", examId);

        return exam.ToDetailDto(ExamModeratorView, await CountExamAttemptsAsync(examId, ct));
    }

    public async Task<ExamDetailDto> RejectExamAsync(
        Guid examId, RejectExamRequest request, CancellationToken ct)
    {
        var exam = await LoadExamForModerationAsync(examId, ct);

        exam.Status = ExamStatus.Rejected;
        exam.ApprovedAt = null;
        exam.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Sınaq rədd edildi. ExamId: {ExamId}", examId);

        return exam.ToDetailDto(ExamModeratorView, await CountExamAttemptsAsync(examId, ct));
    }

    private async Task<Domain.Entities.Exam> LoadExamForModerationAsync(Guid examId, CancellationToken ct) =>
        await _db.Exams
            .Include(e => e.Author)
            .Include(e => e.Sections)
                .ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(e => e.Id == examId, ct)
        ?? throw NotFoundException.For("Sınaq", examId);

    private Task<int> CountExamAttemptsAsync(Guid examId, CancellationToken ct) =>
        _db.ExamAttempts.CountAsync(a => a.ExamId == examId, ct);

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();

        // Rolları tək sorğuda çəkirik - istifadəçi başına ayrıca sorğu (N+1) olmasın deyə.
        var roleRows = await _db.UserRoles
            .AsNoTracking()
            .Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name })
            .ToListAsync(ct);

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName ?? string.Empty).ToList());

        var resourceStats = await _db.Resources
            .AsNoTracking()
            .Where(r => userIds.Contains(r.AuthorId))
            .GroupBy(r => r.AuthorId)
            .Select(g => new
            {
                AuthorId = g.Key,
                Total = g.Count(),
                Approved = g.Count(r => r.Status == ResourceStatus.Approved),
                Downloads = g.Sum(r => r.Downloads)
            })
            .ToListAsync(ct);

        var statsByUser = resourceStats.ToDictionary(x => x.AuthorId);

        var enrollmentCounts = await _db.Enrollments
            .AsNoTracking()
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var certificateCounts = await _db.Certificates
            .AsNoTracking()
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        return users.Select(u =>
        {
            var stats = statsByUser.GetValueOrDefault(u.Id);
            return new AdminUserDto(
                u.Id,
                u.FullName,
                u.Email ?? string.Empty,
                u.Subject,
                rolesByUser.GetValueOrDefault(u.Id, Array.Empty<string>()),
                stats?.Total ?? 0,
                stats?.Approved ?? 0,
                stats?.Downloads ?? 0,
                enrollmentCounts.GetValueOrDefault(u.Id),
                certificateCounts.GetValueOrDefault(u.Id),
                u.CreatedAt);
        }).ToList();
    }

    public async Task<SalesSummaryDto> GetSalesAsync(CancellationToken ct)
    {
        var paidItems = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order!.Status == OrderStatus.Paid)
            .ToListAsync(ct);

        var orderCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Paid, ct);

        return new SalesSummaryDto(
            paidItems.Sum(i => i.Price),
            paidItems.Sum(i => i.CommissionAmount),
            paidItems.Sum(i => i.AuthorPayoutAmount),
            orderCount,
            paidItems.Count,
            paidItems.Where(i => i.ItemType == CatalogItemType.Resource).Sum(i => i.Price),
            paidItems.Where(i => i.ItemType == CatalogItemType.Training).Sum(i => i.Price),
            _commission.ResourcePercent);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(o => o.ToDto()).ToList();
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var since24h = DateTime.UtcNow.AddHours(-24);

        return new AdminDashboardDto(
            await BuildTrafficStatsAsync(since24h, ct),
            await BuildContentStatsAsync(ct),
            await BuildTrainingStatsAsync(ct),
            await BuildSalesStatsAsync(ct),
            await BuildExamStatsAsync(ct),
            await BuildTopErrorsAsync(ct),
            await BuildTopTrainingsAsync(ct),
            DateTime.UtcNow);
    }

    private async Task<AdminTrafficStatsDto> BuildTrafficStatsAsync(DateTime since, CancellationToken ct)
    {
        // Tək qruplaşdırma ilə bütün saylar - hər göstərici üçün ayrıca COUNT getməsin.
        var summary = await _db.RequestLogs
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.LongCount(),
                Last24 = g.LongCount(l => l.CreatedAt >= since),
                Failed = g.LongCount(l => l.StatusCode >= 400),
                ClientErrors = g.LongCount(l => l.StatusCode >= 400 && l.StatusCode < 500),
                ServerErrors = g.LongCount(l => l.StatusCode >= 500),
                ServerErrors24 = g.LongCount(l => l.StatusCode >= 500 && l.CreatedAt >= since),
                AverageDuration = g.Average(l => (double?)l.DurationMs)
            })
            .FirstOrDefaultAsync(ct);

        if (summary is null || summary.Total == 0)
        {
            return new AdminTrafficStatsDto(0, 0, 0, 0, 0, 0, 0, 0);
        }

        return new AdminTrafficStatsDto(
            summary.Total,
            summary.Last24,
            summary.Failed,
            summary.ClientErrors,
            summary.ServerErrors,
            summary.ServerErrors24,
            Math.Round(summary.AverageDuration ?? 0, 1),
            Math.Round(summary.Failed * 100d / summary.Total, 2));
    }

    private async Task<AdminContentStatsDto> BuildContentStatsAsync(CancellationToken ct)
    {
        var resources = await _db.Resources
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(r => r.Status == ResourceStatus.Pending),
                Approved = g.Count(r => r.Status == ResourceStatus.Approved),
                Rejected = g.Count(r => r.Status == ResourceStatus.Rejected),
                Downloads = g.Sum(r => r.Downloads)
            })
            .FirstOrDefaultAsync(ct);

        return new AdminContentStatsDto(
            await _db.Users.CountAsync(ct),
            resources?.Total ?? 0,
            resources?.Pending ?? 0,
            resources?.Approved ?? 0,
            resources?.Rejected ?? 0,
            resources?.Downloads ?? 0,
            await _db.BlogPosts.CountAsync(ct),
            await _db.Certificates.CountAsync(ct));
    }

    private async Task<AdminTrainingStatsDto> BuildTrainingStatsAsync(CancellationToken ct)
    {
        var trainings = await _db.Trainings
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Live = g.Count(t => t.Format == TrainingFormat.Live),
                Online = g.Count(t => t.Format == TrainingFormat.Online),
                Video = g.Count(t => t.Format == TrainingFormat.Video)
            })
            .FirstOrDefaultAsync(ct);

        var enrollments = await _db.Enrollments
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(e => e.Status == EnrollmentStatus.Completed)
            })
            .FirstOrDefaultAsync(ct);

        // "Satılan təlim" = ödənişi tamamlanmış sifarişlərdəki təlim sətirləri.
        // Qeydiyyat sayı bundan fərqlənə bilər (admin əl ilə də yazdıra bilər).
        var sold = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ItemType == CatalogItemType.Training && oi.Order!.Status == OrderStatus.Paid)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Revenue = g.Sum(oi => oi.Price) })
            .FirstOrDefaultAsync(ct);

        return new AdminTrainingStatsDto(
            trainings?.Total ?? 0,
            trainings?.Live ?? 0,
            trainings?.Online ?? 0,
            trainings?.Video ?? 0,
            await _db.TrainingLessons.CountAsync(ct),
            enrollments?.Total ?? 0,
            enrollments?.Completed ?? 0,
            sold?.Count ?? 0,
            sold?.Revenue ?? 0m);
    }

    private async Task<AdminSalesStatsDto> BuildSalesStatsAsync(CancellationToken ct)
    {
        var paid = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order!.Status == OrderStatus.Paid)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Gmv = g.Sum(oi => oi.Price),
                Commission = g.Sum(oi => oi.CommissionAmount),
                Payout = g.Sum(oi => oi.AuthorPayoutAmount),
                SoldResources = g.Count(oi => oi.ItemType == CatalogItemType.Resource),
                ResourceRevenue = g.Sum(oi => oi.ItemType == CatalogItemType.Resource ? oi.Price : 0m)
            })
            .FirstOrDefaultAsync(ct);

        return new AdminSalesStatsDto(
            paid?.Gmv ?? 0m,
            paid?.Commission ?? 0m,
            paid?.Payout ?? 0m,
            await _db.Orders.CountAsync(o => o.Status == OrderStatus.Paid, ct),
            await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct),
            paid?.SoldResources ?? 0,
            paid?.ResourceRevenue ?? 0m);
    }

    /// <summary>Ən çox təkrarlanan uğursuz ünvanlar - problemi tez tapmaq üçün.</summary>
    private async Task<AdminExamStatsDto> BuildExamStatsAsync(CancellationToken ct)
    {
        var exams = await _db.Exams
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(e => e.Status == ExamStatus.Pending),
                Approved = g.Count(e => e.Status == ExamStatus.Approved),
                Rejected = g.Count(e => e.Status == ExamStatus.Rejected)
            })
            .FirstOrDefaultAsync(ct);

        var attempts = await _db.ExamAttempts
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Passed = g.Count(a => a.Passed),
                InProgress = g.Count(a => a.Status == ExamAttemptStatus.InProgress),
                AverageScore = g.Average(a => (double?)a.ScorePercent)
            })
            .FirstOrDefaultAsync(ct);

        var sold = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ItemType == CatalogItemType.Exam && oi.Order!.Status == OrderStatus.Paid)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Revenue = g.Sum(oi => oi.Price) })
            .FirstOrDefaultAsync(ct);

        return new AdminExamStatsDto(
            exams?.Total ?? 0,
            exams?.Pending ?? 0,
            exams?.Approved ?? 0,
            exams?.Rejected ?? 0,
            await _db.ExamQuestions.CountAsync(ct),
            attempts?.Total ?? 0,
            attempts?.InProgress ?? 0,
            attempts?.Passed ?? 0,
            Math.Round(attempts?.AverageScore ?? 0, 1),
            sold?.Count ?? 0,
            sold?.Revenue ?? 0m);
    }

    private async Task<IReadOnlyList<AdminTopErrorDto>> BuildTopErrorsAsync(CancellationToken ct)
    {
        // Qruplaşdırmanı birbaşa record-a proyeksiya etmək EF-də tərcümə olunmur,
        // ona görə əvvəlcə anonim tipə yığılır, DTO isə yaddaşda qurulur.
        var rows = await _db.RequestLogs
            .AsNoTracking()
            .Where(l => l.StatusCode >= 400)
            .GroupBy(l => new { l.Method, l.Path, l.StatusCode })
            .Select(g => new
            {
                g.Key.Method,
                g.Key.Path,
                g.Key.StatusCode,
                Count = g.LongCount(),
                LastOccurredAt = g.Max(l => l.CreatedAt)
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        return rows
            .Select(x => new AdminTopErrorDto(x.Method, x.Path, x.StatusCode, x.Count, x.LastOccurredAt))
            .ToList();
    }

    private async Task<IReadOnlyList<AdminTopTrainingDto>> BuildTopTrainingsAsync(CancellationToken ct)
    {
        var counts = await _db.Enrollments
            .AsNoTracking()
            .GroupBy(e => e.TrainingId)
            .Select(g => new { TrainingId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        if (counts.Count == 0)
        {
            return Array.Empty<AdminTopTrainingDto>();
        }

        var trainingIds = counts.Select(x => x.TrainingId).ToList();

        var names = await _db.Trainings
            .AsNoTracking()
            .Where(t => trainingIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var revenue = await _db.OrderItems
            .AsNoTracking()
            .Where(oi =>
                oi.ItemType == CatalogItemType.Training &&
                trainingIds.Contains(oi.ItemId) &&
                oi.Order!.Status == OrderStatus.Paid)
            .GroupBy(oi => oi.ItemId)
            .Select(g => new { TrainingId = g.Key, Total = g.Sum(oi => oi.Price) })
            .ToDictionaryAsync(x => x.TrainingId, x => x.Total, ct);

        return counts
            .Select(x => new AdminTopTrainingDto(
                x.TrainingId,
                names.GetValueOrDefault(x.TrainingId, "Silinmiş təlim"),
                x.Count,
                revenue.GetValueOrDefault(x.TrainingId)))
            .ToList();
    }

    public async Task<PagedResult<RequestLogDto>> GetRequestLogsAsync(RequestLogQuery query, CancellationToken ct)
    {
        var q = _db.RequestLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Method))
        {
            var method = query.Method.Trim().ToUpper();
            q = q.Where(l => l.Method == method);
        }

        if (!string.IsNullOrWhiteSpace(query.Path))
        {
            var path = query.Path.Trim().ToLower();
            q = q.Where(l => l.Path.ToLower().Contains(path));
        }

        if (query.StatusCode is > 0)
        {
            q = q.Where(l => l.StatusCode == query.StatusCode);
        }

        if (query.UserId is not null)
        {
            q = q.Where(l => l.UserId == query.UserId);
        }

        if (query.OnlyErrors == true)
        {
            q = q.Where(l => l.StatusCode >= 400);
        }

        // Query string-dən gələn tarixdə "Z" yoxdursa Kind=Unspecified olur və Npgsql
        // onu timestamptz sütunu ilə müqayisə edə bilmir - UTC kimi normallaşdırırıq.
        if (query.From is not null)
        {
            var from = ToUtc(query.From.Value);
            q = q.Where(l => l.CreatedAt >= from);
        }

        if (query.To is not null)
        {
            var to = ToUtc(query.To.Value);
            q = q.Where(l => l.CreatedAt <= to);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RequestLogDto>
        {
            Items = items.Select(l => new RequestLogDto(
                l.Id, l.TraceId, l.Method, l.Path, l.QueryString,
                l.StatusCode, l.DurationMs, l.UserId, l.UserEmail,
                l.IpAddress, l.UserAgent, l.RequestContentType,
                l.RequestBody, l.ResponseBody, l.ExceptionType, l.CreatedAt)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private async Task<Domain.Entities.Resource> LoadForModerationAsync(Guid resourceId, CancellationToken ct) =>
        await _db.Resources
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct)
        ?? throw NotFoundException.For("Resurs", resourceId);
}
