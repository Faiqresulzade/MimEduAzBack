using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Mappings;

public static class ResourceMappings
{
    public static ResourceDto ToDto(this Resource r) => new(
        r.Id,
        r.Name,
        r.Subject,
        r.Grade,
        r.Type,
        r.AuthorId,
        r.Author?.FullName ?? string.Empty,
        r.Downloads,
        r.IsPaid,
        r.Price,
        r.Status,
        r.Quiz is not null,
        r.CreatedAt,
        r.ApprovedAt);

    public static ResourceDetailDto ToDetailDto(this Resource r) => new(
        r.Id,
        r.Name,
        r.Subject,
        r.Grade,
        r.Type,
        r.AuthorId,
        r.Author?.FullName ?? string.Empty,
        r.Author?.Subject,
        r.Downloads,
        r.IsPaid,
        r.Price,
        r.Status,
        r.RejectionReason,
        r.Quiz is not null,
        r.Quiz?.Id,
        r.OriginalFileName,
        r.CreatedAt,
        r.ApprovedAt);
}
