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
    /// <summary>Fayl yox, xarici link əsaslıdır (Video / ExternalLink).</summary>
    bool IsLinkBased,
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
    bool IsLinkBased,
    /// <summary>
    /// Yalnız PULSUZ link resurslarında doldurulur (məs. video embed üçün).
    /// Ödənişli videoda null qalır - link "download" endpoint-i ilə alınır.
    /// </summary>
    string? ExternalUrl,
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
    /// <summary>
    /// Fayl resurslarında serverdəki fayl yolu, link resurslarında isə xarici ünvan.
    /// <see cref="IsExternal"/> true olanda yeni tabda açılmalıdır, endirilməməlidir.
    /// </summary>
    string DownloadUrl,
    bool IsExternal,
    int Downloads);

/// <summary>Müəllif profili (ictimai).</summary>
public sealed record AuthorProfileDto(
    Guid Id,
    string FullName,
    string? Subject,
    int ResourceCount,
    int TotalDownloads,
    IReadOnlyList<ResourceDto> Resources);

/// <summary>
/// Fayl yüklənmədən link əsaslı resurs yaratmaq (video dərs və ya xarici material).
/// Fayl əsaslı tiplər üçün multipart endpoint istifadə olunur.
/// </summary>
public sealed class CreateResourceLinkRequest
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int Grade { get; set; }

    /// <summary>Yalnız <c>Video</c> və ya <c>ExternalLink</c> ola bilər.</summary>
    public ResourceType Type { get; set; } = ResourceType.Video;

    /// <summary>Materialın ünvanı (http/https).</summary>
    public string ExternalUrl { get; set; } = string.Empty;

    /// <summary><c>ExternalLink</c> tipində həmişə false olmalıdır.</summary>
    public bool IsPaid { get; set; }

    public decimal Price { get; set; }
}
