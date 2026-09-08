using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

/// <summary>Resurs Bankı filtri.</summary>
public sealed class ResourceQuery
{
    public string? Subject { get; set; }
    public int? Grade { get; set; }
    public ResourceType? Type { get; set; }
    public ResourceStatus? Status { get; set; }
    public bool? IsPaid { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public interface IResourceService
{
    Task<PagedResult<ResourceDto>> GetAsync(ResourceQuery query, CancellationToken ct);
    Task<ResourceDetailDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ResourceDetailDto> CreateAsync(CreateResourceRequest request, ResourceFileUpload file, CancellationToken ct);

    /// <summary>
    /// Fayl yüklənmədən link əsaslı resurs yaradır (video dərs və ya xarici material).
    /// Digər tiplər kimi moderasiyaya (Pending) düşür.
    /// </summary>
    Task<ResourceDetailDto> CreateLinkAsync(CreateResourceLinkRequest request, CancellationToken ct);
    Task<ResourceDownloadDto> DownloadAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ResourceDto>> GetMineAsync(CancellationToken ct);
    Task<AuthorProfileDto> GetAuthorProfileAsync(Guid authorId, CancellationToken ct);
}
