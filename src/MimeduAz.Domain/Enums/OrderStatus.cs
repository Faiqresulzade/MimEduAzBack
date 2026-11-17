namespace MimeduAz.Domain.Enums;

/// <summary>
/// Sifarişin vəziyyəti. Ödənişli sifariş <see cref="Pending"/> yaradılır və yalnız AzeriCard-ın
/// imzası yoxlanmış təsdiqi ilə <see cref="Paid"/> olur. Məhsula giriş yalnız <see cref="Paid"/>-də açılır.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    /// <summary>24 saat ərzində ödənilməyən sifariş.</summary>
    Cancelled = 2,
    Refunded = 3,
    /// <summary>Bank ödənişi rədd etdi və ya emal xətası oldu.</summary>
    Failed = 4
}
