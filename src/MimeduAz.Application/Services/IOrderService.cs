using MimeduAz.Contracts.Orders;

namespace MimeduAz.Application.Services;

public interface IOrderService
{
    /// <summary>
    /// Səbəti sifarişə çevirir. Demo rejim: heç bir xarici ödəniş provayderi çağırılmır,
    /// sifariş birbaşa "Paid" statusunda yaradılır.
    /// </summary>
    Task<OrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct);

    Task<IReadOnlyList<OrderDto>> GetMineAsync(CancellationToken ct);
}
