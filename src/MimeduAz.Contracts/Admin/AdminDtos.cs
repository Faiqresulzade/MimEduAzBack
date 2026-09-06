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
