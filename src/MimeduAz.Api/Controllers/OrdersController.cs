using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Orders;

namespace MimeduAz.Api.Controllers;

/// <summary>Sifarişlər və checkout.</summary>
[ApiController]
[Route("api/v1/orders")]
[Authorize]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>
    /// Səbəti sifarişə çevirir. Pulsuz səbət dərhal tamamlanır (<c>payment: null</c>).
    /// Ödənişli səbətdə sifariş Pending yaradılır və <c>payment</c> formu qaytarılır — frontend
    /// istifadəçini onunla AzeriCard səhifəsinə POST edir. Giriş yalnız bank təsdiqindən sonra açılır.
    /// Kart məlumatı qəbul edilmir. Kartla ödəniş söndürülübsə ödənişli səbət 409 alır.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(CheckoutRequest? request, CancellationToken ct)
    {
        var result = await _orders.CheckoutAsync(request ?? new CheckoutRequest(), ct);
        return Created($"/api/v1/orders/{result.Order.Code}", result);
    }

    /// <summary>Cari istifadəçinin sifariş tarixçəsi (qəbz məlumatı ilə).</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Mine(CancellationToken ct) =>
        Ok(await _orders.GetMineAsync(ct));

    /// <summary>Ödəniş nəticəsi səhifəsi üçün: sifarişin statusu (yalnız sahibinə).</summary>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Get(string code, CancellationToken ct) =>
        Ok(await _orders.GetMineByCodeAsync(code, ct));
}
