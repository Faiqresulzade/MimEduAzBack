using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Payouts;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>Müəllif qazancı və bank köçürməsi ilə çıxarılması (kart nömrəsi toplanmır).</summary>
[ApiController]
[Route("api/v1/payouts")]
[Authorize(Roles = AppRoles.AuthorRoles)]
[Produces("application/json")]
public sealed class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payouts;

    public PayoutsController(IPayoutService payouts) => _payouts = payouts;

    /// <summary>Qazanc (yalnız ödənilmiş satışlardan), balans və tələblər.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(EarningsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EarningsDto>> Mine(CancellationToken ct) =>
        Ok(await _payouts.GetMyEarningsAsync(ct));

    /// <summary>Bütün mövcud balansın IBAN-a köçürülməsini tələb edir (min. 20 ₼).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PayoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayoutDto>> Create(CreatePayoutRequest request, CancellationToken ct) =>
        Ok(await _payouts.RequestAsync(request, ct));
}
