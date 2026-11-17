using MimeduAz.Contracts.Orders;

namespace MimeduAz.Application.Services;

public interface IOrderService
{
    /// <summary>
    /// Səbəti sifarişə çevirir. Pulsuz səbət dərhal tamamlanır. Ödənişli səbət Pending sifariş
    /// yaradır və AzeriCard səhifəsinə yönləndirmə formu qaytarır; giriş bank təsdiqindən sonra açılır.
    /// Kartla ödəniş söndürülübsə ödənişli checkout 409 qaytarır.
    /// </summary>
    Task<CheckoutResponse> CheckoutAsync(CheckoutRequest request, CancellationToken ct);

    Task<IReadOnlyList<OrderDto>> GetMineAsync(CancellationToken ct);

    /// <summary>Ödəniş nəticəsi səhifəsi üçün: cari istifadəçinin sifarişi (kodla).</summary>
    Task<OrderDto> GetMineByCodeAsync(string code, CancellationToken ct);
}
