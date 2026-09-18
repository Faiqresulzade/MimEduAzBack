namespace MimeduAz.Contracts.Admin;

public sealed class RejectResourceRequest
{
    public string? Reason { get; set; }
}

/// <summary>Admin panelində müəllim sətri + statistikası.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string FullName,
    string Email,
    string? Subject,
    IReadOnlyList<string> Roles,
    int ResourceCount,
    int ApprovedResourceCount,
    int TotalDownloads,
    int EnrollmentCount,
    int CertificateCount,
    DateTime CreatedAt);

/// <summary>Satış xülasəsi. GMV — ümumi dövriyyə, komissiya platformanın payı.</summary>
public sealed record SalesSummaryDto(
    decimal Gmv,
    decimal CommissionTotal,
    decimal AuthorPayoutTotal,
    int OrderCount,
    int ItemCount,
    decimal ResourceRevenue,
    decimal TrainingRevenue,
    decimal CommissionPercent);

/// <summary>Audit log sətri (admin panelində sorğu tarixçəsi).</summary>
public sealed record RequestLogDto(
    long Id,
    string TraceId,
    string Method,
    string Path,
    string? QueryString,
    int StatusCode,
    int DurationMs,
    Guid? UserId,
    string? UserEmail,
    string? IpAddress,
    string? UserAgent,
    string? RequestContentType,
    string? RequestBody,
    string? ResponseBody,
    string? ExceptionType,
    DateTime CreatedAt);

/// <summary>
/// Admin panelinin baş səhifəsi üçün icmal.
/// Bütün saylar bir sorğuda qaytarılır ki, panel açılanda 10 ayrı çağırış olmasın.
/// </summary>
public sealed record AdminDashboardDto(
    AdminTrafficStatsDto Traffic,
    AdminContentStatsDto Content,
    AdminTrainingStatsDto Trainings,
    AdminSalesStatsDto Sales,
    IReadOnlyList<AdminTopErrorDto> TopErrors,
    IReadOnlyList<AdminTopTrainingDto> TopTrainings,
    DateTime GeneratedAt);

/// <summary>Audit log-dan hesablanan sorğu statistikası.</summary>
public sealed record AdminTrafficStatsDto(
    long TotalRequests,
    long RequestsLast24Hours,
    long FailedRequests,
    long ClientErrors,
    long ServerErrors,
    long ServerErrorsLast24Hours,
    double AverageDurationMs,
    /// <summary>Uğursuz sorğuların ümumi sorğulara nisbəti (%).</summary>
    double ErrorRatePercent);

/// <summary>Resurs, istifadəçi və bloq sayları.</summary>
public sealed record AdminContentStatsDto(
    int TotalUsers,
    int TotalResources,
    int PendingResources,
    int ApprovedResources,
    int RejectedResources,
    int TotalDownloads,
    int BlogPosts,
    int IssuedCertificates);

/// <summary>Təlim və qeydiyyat sayları.</summary>
public sealed record AdminTrainingStatsDto(
    int TotalTrainings,
    /// <summary>Formata görə bölgü: Live / Online / Video.</summary>
    int LiveTrainings,
    int OnlineTrainings,
    int VideoTrainings,
    int TotalLessons,
    int TotalEnrollments,
    int CompletedEnrollments,
    /// <summary>Ödənişi tamamlanmış sifarişlərdə satılan təlim sayı.</summary>
    int SoldTrainings,
    decimal TrainingRevenue);

/// <summary>Satış göstəriciləri (yalnız ödənişi tamamlanmış sifarişlər).</summary>
public sealed record AdminSalesStatsDto(
    decimal Gmv,
    decimal CommissionTotal,
    decimal AuthorPayoutTotal,
    int PaidOrders,
    int PendingOrders,
    int SoldResources,
    decimal ResourceRevenue);

/// <summary>Ən çox təkrarlanan xəta ünvanları.</summary>
public sealed record AdminTopErrorDto(
    string Method,
    string Path,
    int StatusCode,
    long Count,
    DateTime LastOccurredAt);

/// <summary>Ən çox satılan təlimlər.</summary>
public sealed record AdminTopTrainingDto(
    Guid TrainingId,
    string Name,
    int EnrollmentCount,
    decimal Revenue);
