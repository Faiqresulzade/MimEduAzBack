using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Orders;
using MimeduAz.Contracts.Payouts;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>Admin: müəllif ödəniş tələbləri və sifarişin geri qaytarılması.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = AppRoles.Admin)]
[Produces("application/json")]
public sealed class AdminPaymentsController : ControllerBase
{
    private readonly IPayoutService _payouts;
    private readonly IPaymentService _payments;

    public AdminPaymentsController(IPayoutService payouts, IPaymentService payments)
    {
        _payouts = payouts;
        _payments = payments;
    }

    /// <summary>Bütün ödəniş tələbləri (gözləyənlər əvvəldə). Köçürmə üçün tam IBAN və FİN görünür.</summary>
    [HttpGet("payouts")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminPayoutDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPayoutDto>>> Payouts(CancellationToken ct) =>
        Ok(await _payouts.GetAllAsync(ct));

    /// <summary>Tələbi "köçürüldü" və ya "rədd edildi" kimi qeyd edir.</summary>
    [HttpPost("payouts/{id:guid}/process")]
    [ProducesResponseType(typeof(AdminPayoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminPayoutDto>> ProcessPayout(
        Guid id, ProcessPayoutRequest request, CancellationToken ct) =>
        Ok(await _payouts.ProcessAsync(id, request, ct));

    /// <summary>
    /// Ödənişi kartla geri qaytarır (AzeriCard TRTYPE=22). Giriş bağlanır, müəllif payı balansdan çıxır.
    /// </summary>
    [HttpPost("orders/{id:guid}/refund")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<OrderDto>> Refund(Guid id, CancellationToken ct) =>
        Ok(await _payments.RefundAsync(id, ct));
}
