using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Orders;

public sealed record OrderItemDto(
    Guid Id,
    CatalogItemType ItemType,
    Guid ItemId,
    string Name,
    decimal Price);

/// <summary>Sifariş və qəbz məlumatı. Kart nömrəsi yalnız bankın qaytardığı maskalı formada.</summary>
public sealed record OrderDto(
    Guid Id,
    string Code,
    string CustomerName,
    decimal TotalAmount,
    OrderStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items,
    DateTime? PaidAt = null,
    string? CardMask = null,
    string? Rrn = null,
    string? ApprovalCode = null,
    string? PaymentResult = null);

/// <summary>
/// Checkout. Kart məlumatları QƏBUL EDİLMİR — ödəniş AzeriCard-ın səhifəsində aparılır.
/// </summary>
public sealed class CheckoutRequest
{
    /// <summary>Opsional: sifarişdə göstəriləcək ad. Boş olarsa istifadəçinin adı götürülür.</summary>
    public string? CustomerName { get; set; }

    /// <summary>İstifadə şərtləri, geri qaytarma və xidmətin təqdimi qaydaları ilə razılıq. Ödənişli sifarişdə məcburidir.</summary>
    public bool AcceptTerms { get; set; }

    /// <summary>3-D Secure 2 üçün brauzer məlumatı (Base64 JSON: ekran ölçüsü, saat qurşağı).</summary>
    public string? ClientInfo { get; set; }
}

/// <summary>Bank səhifəsinə avtomatik POST ediləcək form.</summary>
public sealed record PaymentRedirectDto(string Url, IReadOnlyDictionary<string, string> Fields);

/// <summary>
/// Pulsuz səbət dərhal tamamlanır (<see cref="Payment"/> null). Ödənişli səbətdə sifariş
/// Pending-dir və frontend istifadəçini <see cref="Payment"/> formu ilə banka göndərir.
/// </summary>
public sealed record CheckoutResponse(OrderDto Order, PaymentRedirectDto? Payment);

/// <summary>Frontend üçün: kartla ödəniş aktivdirmi.</summary>
public sealed record PaymentConfigDto(bool Enabled, string Currency, string TermsVersion);
