using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Payouts;

/// <summary>Bir materialın satışından müəllifə düşən pay.</summary>
public sealed record EarningItemDto(
    CatalogItemType ItemType,
    Guid ItemId,
    string Name,
    int UnitsSold,
    decimal Gross,
    decimal Net);

/// <summary>
/// Müəllifin qazancı: yalnız ödənilmiş (Paid) sifarişlərdən. Qaytarılan sifariş avtomatik çıxılır.
/// <see cref="Available"/> = Net − (ödənilmiş + gözləyən tələblər).
/// </summary>
public sealed record EarningsDto(
    decimal Gross,
    decimal Commission,
    decimal Net,
    decimal PaidOut,
    decimal PendingPayout,
    decimal Available,
    decimal MinimumPayout,
    IReadOnlyList<EarningItemDto> Items,
    IReadOnlyList<PayoutDto> Payouts);

public sealed record PayoutDto(
    Guid Id,
    decimal Amount,
    /// <summary>Maskalı IBAN, məs. AZ21 **** **** 1234.</summary>
    string IbanMasked,
    string AccountHolder,
    PayoutStatus Status,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

/// <summary>Admin üçün: köçürmə etmək üçün tam IBAN və FİN görünür.</summary>
public sealed record AdminPayoutDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string UserEmail,
    decimal Amount,
    string Iban,
    string AccountHolder,
    string Fin,
    PayoutStatus Status,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

/// <summary>Müəllim bütün mövcud balansı tələb edir. Kart nömrəsi qəbul edilmir.</summary>
public sealed class CreatePayoutRequest
{
    public string Iban { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string Fin { get; set; } = string.Empty;
}

public sealed class ProcessPayoutRequest
{
    /// <summary>true - köçürülüb, false - rədd edilib.</summary>
    public bool Approve { get; set; }

    /// <summary>Köçürmənin referansı və ya rədd səbəbi.</summary>
    public string? Note { get; set; }
}
