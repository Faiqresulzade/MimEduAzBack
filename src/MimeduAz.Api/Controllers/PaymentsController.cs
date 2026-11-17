using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Orders;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Api.Controllers;

/// <summary>Kartla ödəniş (AzeriCard).</summary>
[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly PaymentOptions _options;

    public PaymentsController(IPaymentService payments, IOptions<PaymentOptions> options)
    {
        _payments = payments;
        _options = options.Value;
    }

    /// <summary>Kartla ödəniş aktivdirmi (frontend "Kartla ödə" düyməsini buna görə göstərir).</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PaymentConfigDto), StatusCodes.Status200OK)]
    public ActionResult<PaymentConfigDto> Config() => Ok(_payments.GetConfig());

    /// <summary>
    /// AzeriCard BACKREF: bank ödəniş nəticəsini bura göndərir (brauzer POST-u). İmza yoxlanılır,
    /// nəticə sifarişə yazılır və istifadəçi frontend-in nəticə səhifəsinə yönləndirilir.
    /// Eyni nəticənin təkrar gəlməsi heç nəyi dəyişmir.
    /// </summary>
    [HttpPost("azericard/callback")]
    [HttpGet("azericard/callback")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ApiExplorerSettings(IgnoreApi = false)]
    public async Task<IActionResult> AzeriCardCallback(CancellationToken ct)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in Request.Query)
        {
            fields[key] = value.ToString();
        }

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            foreach (var (key, value) in form)
            {
                fields[key] = value.ToString();
            }
        }

        var outcome = await _payments.HandleCallbackAsync(fields, ct);

        var site = _options.SiteUrl.TrimEnd('/');
        var page = outcome.Status switch
        {
            OrderStatus.Paid => "ugurlu",
            OrderStatus.Failed or OrderStatus.Cancelled => "ugursuz",
            // Pending: tamamlama gözləyir - nəticə səhifəsi statusu yoxlamağa davam edir.
            _ => outcome.OrderCode is null ? "ugursuz" : "ugurlu"
        };
        var query = outcome.OrderCode is null ? string.Empty : $"?order={Uri.EscapeDataString(outcome.OrderCode)}";

        // 303: brauzer POST-dan sonra frontend səhifəsini GET ilə açır.
        Response.Headers.Location = $"{site}/odenis/{page}{query}";
        return StatusCode(StatusCodes.Status303SeeOther);
    }
}
