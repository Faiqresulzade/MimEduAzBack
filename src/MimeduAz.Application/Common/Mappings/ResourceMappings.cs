using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Mappings;

/// <summary>
/// Resursun cari istifadəçiyə görə dəyişən görünüşü.
/// Bir yerdə saxlanılır ki, «ödənişli linki kim görə bilər» qaydası
/// siyahı və detal cavablarında fərqlənməsin.
/// </summary>
public sealed record ResourceViewContext(bool IsPurchased, bool IsAuthor, bool IsAdmin)
{
    public static readonly ResourceViewContext Anonymous = new(false, false, false);

    /// <summary>Ödənişli resursun məzmununa çıxışı olanlar.</summary>
    public bool HasPaidAccess => IsPurchased || IsAuthor || IsAdmin;
}

public static class ResourceMappings
{
    public static ResourceDto ToDto(this Resource r, ResourceViewContext? view = null)
    {
        var context = view ?? ResourceViewContext.Anonymous;

        return new ResourceDto(
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
            r.IsLinkBased,
            context.IsPurchased,
            CanAccess(r, context),
            r.CreatedAt,
            r.ApprovedAt);
    }

    public static ResourceDetailDto ToDetailDto(this Resource r, ResourceViewContext? view = null)
    {
        var context = view ?? ResourceViewContext.Anonymous;

        return new ResourceDetailDto(
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
            r.IsLinkBased,
            // Ödənişli linki yalnız çıxışı olanlar görür. Admin də daxildir -
            // əks halda moderator videoya baxmadan təsdiqləmək məcburiyyətində qalır.
            r.IsLinkBased && (!r.IsPaid || context.HasPaidAccess) ? r.ExternalUrl : null,
            context.IsPurchased,
            CanAccess(r, context),
            r.CreatedAt,
            r.ApprovedAt);
    }

    private static bool CanAccess(Resource r, ResourceViewContext context) =>
        !r.IsPaid || context.HasPaidAccess;
}
