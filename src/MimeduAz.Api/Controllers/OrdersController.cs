using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Orders;

namespace MimeduAz.Api.Controllers;

/// <summary>Sifarişlər və demo checkout.</summary>
[ApiController]
[Route("api/v1/orders")]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>
    /// Səbəti sifarişə çevirir və "Paid" statusunda tamamlayır (demo ödəniş).
    /// Təlim sətirləri üçün avtomatik enrollment yaradılır, səbət boşaldılır.
    /// DİQQƏT: bu endpoint heç bir kart məlumatı qəbul etmir və saxlamır.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Checkout(CheckoutRequest? request, CancellationToken ct)
    {
        var order = await _orders.CheckoutAsync(request ?? new CheckoutRequest(), ct);
        return Created($"/api/v1/orders/{order.Id}", order);
    }

    /// <summary>Cari istifadəçinin sifariş tarixçəsi.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Mine(CancellationToken ct) =>
        Ok(await _orders.GetMineAsync(ct));
}
