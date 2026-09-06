using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Carts;

public sealed record CartItemDto(
    Guid Id,
    CatalogItemType ItemType,
    Guid ItemId,
    string Name,
    decimal Price,
    DateTime AddedAt);

public sealed record CartDto(
    Guid Id,
    IReadOnlyList<CartItemDto> Items,
    decimal Total);

public sealed record AddCartItemRequest(CatalogItemType ItemType, Guid ItemId);
