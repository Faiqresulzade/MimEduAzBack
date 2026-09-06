namespace MimeduAz.Domain.Enums;

/// <summary>Sifarişin vəziyyəti. Demo rejimdə yalnız <see cref="Paid"/> istifadə olunur.</summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Cancelled = 2,
    Refunded = 3
}
