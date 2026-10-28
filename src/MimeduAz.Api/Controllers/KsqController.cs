using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Ksq;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>
/// KSQ cavab kartı yoxlaması: açar (testlər), AI ilə kart oxuma və nəticələr.
/// Hər müəllim yalnız öz testlərini və nəticələrini görür.
/// </summary>
[ApiController]
[Route("api/v1/ksq")]
[Authorize(Roles = AppRoles.AuthorRoles)]
[Produces("application/json")]
public sealed class KsqController : ControllerBase
{
    private readonly IKsqService _ksq;

    public KsqController(IKsqService ksq) => _ksq = ksq;

    // ------------------------------------------------------------ Testlər

    /// <summary>Saxlanmış testlər (yaradılma sırası ilə).</summary>
    [HttpGet("tests")]
    [ProducesResponseType(typeof(IReadOnlyList<KsqTestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KsqTestDto>>> GetTests(CancellationToken ct) =>
        Ok(await _ksq.GetTestsAsync(ct));

    [HttpPost("tests")]
    [ProducesResponseType(typeof(KsqTestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KsqTestDto>> CreateTest(SaveKsqTestRequest request, CancellationToken ct) =>
        Ok(await _ksq.CreateTestAsync(request, ct));

    [HttpPut("tests/{id:guid}")]
    [ProducesResponseType(typeof(KsqTestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KsqTestDto>> UpdateTest(
        Guid id, SaveKsqTestRequest request, CancellationToken ct) =>
        Ok(await _ksq.UpdateTestAsync(id, request, ct));

    /// <summary>Testi və onun bütün nəticələrini silir.</summary>
    [HttpDelete("tests/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTest(Guid id, CancellationToken ct)
    {
        await _ksq.DeleteTestAsync(id, ct);
        return NoContent();
    }

    // ------------------------------------------------------------ Nəticələr

    /// <summary>Nəticələr (ən yenisi birinci) və filtr üçün siniflər.</summary>
    [HttpGet("results")]
    [ProducesResponseType(typeof(KsqResultListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KsqResultListDto>> GetResults(
        [FromQuery] Guid? testId, [FromQuery] string? sinif, CancellationToken ct) =>
        Ok(await _ksq.GetResultsAsync(testId, sinif, ct));

    /// <summary>Bir nəticəni saxlayır. Bal serverdə açara görə hesablanır.</summary>
    [HttpPost("results")]
    [ProducesResponseType(typeof(KsqResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KsqResultDto>> SaveResult(SaveKsqResultRequest request, CancellationToken ct) =>
        Ok(await _ksq.SaveResultAsync(request, ct));

    /// <summary>"Hamısını saxla" — hamısı ya saxlanır, ya heç biri.</summary>
    [HttpPost("results/bulk")]
    [ProducesResponseType(typeof(IReadOnlyList<KsqResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<KsqResultDto>>> SaveResults(
        SaveKsqResultsBulkRequest request, CancellationToken ct) =>
        Ok(await _ksq.SaveResultsAsync(request, ct));

    [HttpDelete("results/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResult(Guid id, CancellationToken ct)
    {
        await _ksq.DeleteResultAsync(id, ct);
        return NoContent();
    }

    // ------------------------------------------------------------ Kart oxuma

    /// <summary>
    /// Kart şəklini AI ilə oxuyur (multipart: <c>image</c>, <c>closedCount</c>, <c>openCount</c>).
    /// Şəkil saxlanmır. Limit: dəqiqədə 20, sutkada 500 şəkil (429).
    /// </summary>
    [HttpPost("scan")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(KsqScanResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<KsqScanResultDto>> Scan(
        IFormFile image, [FromForm] int closedCount, [FromForm] int openCount, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Şəkil göndərilməlidir.",
                Errors = new Dictionary<string, string[]> { ["image"] = new[] { "Şəkil seçilməyib." } },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        await using var stream = image.OpenReadStream();
        return Ok(await _ksq.ScanAsync(stream, image.ContentType, image.Length, closedCount, openCount, ct));
    }
}
