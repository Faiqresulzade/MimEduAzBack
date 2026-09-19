using MimeduAz.Contracts.Admin;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Exams;
using MimeduAz.Contracts.Orders;
using MimeduAz.Contracts.Resources;

namespace MimeduAz.Application.Services;

/// <summary>Audit log siyahısı üçün filtr.</summary>
public sealed class RequestLogQuery
{
    public string? Method { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>Yalnız 4xx/5xx cavabları göstər.</summary>
    public bool? OnlyErrors { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public interface IAdminService
{
    Task<PagedResult<RequestLogDto>> GetRequestLogsAsync(RequestLogQuery query, CancellationToken ct);
    /// <summary>
    /// Moderasiya növbəsi. Detal DTO qaytarılır ki, moderator təsdiqdən əvvəl
    /// video/xarici linki və fayl adını görə bilsin.
    /// </summary>
    Task<IReadOnlyList<ResourceDetailDto>> GetPendingResourcesAsync(CancellationToken ct);
    Task<ResourceDetailDto> ApproveResourceAsync(Guid resourceId, CancellationToken ct);
    Task<ResourceDetailDto> RejectResourceAsync(Guid resourceId, RejectResourceRequest request, CancellationToken ct);
    /// <summary>Moderasiya gözləyən sınaqlar (bölmə və sual sayı ilə).</summary>
    Task<IReadOnlyList<ExamDetailDto>> GetPendingExamsAsync(CancellationToken ct);

    Task<ExamDetailDto> ApproveExamAsync(Guid examId, CancellationToken ct);
    Task<ExamDetailDto> RejectExamAsync(Guid examId, RejectExamRequest request, CancellationToken ct);

    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken ct);
    Task<SalesSummaryDto> GetSalesAsync(CancellationToken ct);
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct);

    /// <summary>Admin panelinin baş səhifəsi üçün bütün icmal göstəriciləri.</summary>
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct);
}
