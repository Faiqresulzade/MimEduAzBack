using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Orders;

public sealed record OrderItemDto(
    Guid Id,
    CatalogItemType ItemType,
    Guid ItemId,
    string Name,
    decimal Price);

public sealed record OrderDto(
    Guid Id,
    string Code,
    string CustomerName,
    decimal TotalAmount,
    OrderStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);

/// <summary>
/// Demo checkout sorğusu. Kart məlumatları QƏBUL EDİLMİR —
/// ödəniş simulyasiyası tamamilə frontend-dədir, backend yalnız səbəti sifarişə çevirir.
/// </summary>
public sealed class CheckoutRequest
{
    /// <summary>Opsional: sifarişdə göstəriləcək ad. Boş olarsa istifadəçinin adı götürülür.</summary>
    public string? CustomerName { get; set; }
}
