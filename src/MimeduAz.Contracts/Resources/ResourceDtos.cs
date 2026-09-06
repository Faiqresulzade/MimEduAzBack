using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Resources;

/// <summary>Resurs Bankı siyahısı üçün qısa model.</summary>
public sealed record ResourceDto(
    Guid Id,
    string Name,
    string Subject,
    int Grade,
    ResourceType Type,
    Guid AuthorId,
    string AuthorName,
    int Downloads,
    bool IsPaid,
    decimal Price,
    ResourceStatus Status,
    bool HasQuiz,
    DateTime CreatedAt,
    DateTime? ApprovedAt);

/// <summary>Resursun detal səhifəsi üçün model.</summary>
public sealed record ResourceDetailDto(
    Guid Id,
    string Name,
    string Subject,
    int Grade,
    ResourceType Type,
    Guid AuthorId,
    string AuthorName,
    string? AuthorSubject,
    int Downloads,
    bool IsPaid,
    decimal Price,
    ResourceStatus Status,
    string? RejectionReason,
    bool HasQuiz,
    Guid? QuizId,
    string? OriginalFileName,
    DateTime CreatedAt,
    DateTime? ApprovedAt);

/// <summary>Resurs yükləmə sorğusu (multipart/form-data ilə fayl birlikdə göndərilir).</summary>
public sealed class CreateResourceRequest
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int Grade { get; set; }
    public ResourceType Type { get; set; }
    public bool IsPaid { get; set; }
    public decimal Price { get; set; }
}

/// <summary>Fayl yükləmə üçün servis qatına ötürülən model (ASP.NET tiplərindən asılı deyil).</summary>
public sealed record ResourceFileUpload(Stream Content, string FileName, long Length, string? ContentType);

/// <summary>Endirmə cavabı — fayla birbaşa link və yenilənmiş endirmə sayı.</summary>
public sealed record ResourceDownloadDto(
    Guid ResourceId,
    string FileName,
    string DownloadUrl,
    int Downloads);

/// <summary>Müəllif profili (ictimai).</summary>
public sealed record AuthorProfileDto(
    Guid Id,
    string FullName,
    string? Subject,
    int ResourceCount,
    int TotalDownloads,
    IReadOnlyList<ResourceDto> Resources);
