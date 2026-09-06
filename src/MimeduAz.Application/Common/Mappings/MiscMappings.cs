using MimeduAz.Contracts.Blog;
using MimeduAz.Contracts.Carts;
using MimeduAz.Contracts.Certificates;
using MimeduAz.Contracts.Orders;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Mappings;

public static class MiscMappings
{
    public static OrderDto ToDto(this Order o) => new(
        o.Id,
        o.Code,
        o.CustomerName,
        o.TotalAmount,
        o.Status,
        o.CreatedAt,
        o.Items
            .Select(i => new OrderItemDto(i.Id, i.ItemType, i.ItemId, i.Name, i.Price))
            .ToList());

    public static CertificateDto ToDto(this Certificate c) => new(
        c.Id,
        c.Code,
        c.User?.FullName ?? string.Empty,
        c.Description,
        c.TrainingId,
        c.Training?.Name,
        c.IssuedAt);

    public static BlogPostDto ToDto(this BlogPost p) => new(
        p.Id, p.Title, p.Tag, p.ReadTime, p.Excerpt, p.CreatedAt);

    public static BlogPostDetailDto ToDetailDto(this BlogPost p) => new(
        p.Id, p.Title, p.Tag, p.ReadTime, p.Excerpt, p.Body, p.CreatedAt);

    public static CartItemDto ToDto(this CartItem item, string name) => new(
        item.Id, item.ItemType, item.ItemId, name, item.Price, item.AddedAt);
}
