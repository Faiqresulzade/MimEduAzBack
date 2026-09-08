using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Contracts.Certificates;
using MimeduAz.Contracts.Common;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>Sertifikatlar və publik doğrulama.</summary>
[ApiController]
[Route("api/v1/certificates")]
[Produces("application/json")]
public sealed class CertificatesController : ControllerBase
{
    private readonly ICertificateService _certificates;

    public CertificatesController(ICertificateService certificates) => _certificates = certificates;

    /// <summary>Cari istifadəçinin sertifikatları.</summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<CertificateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CertificateDto>>> Mine(CancellationToken ct) =>
        Ok(await _certificates.GetMineAsync(ct));

    /// <summary>
    /// Sertifikatı kod ilə doğrulayır. Publikdir — autentifikasiya tələb olunmur.
    /// Kod tapılmasa da 200 qaytarır, <c>isValid: false</c> ilə.
    /// </summary>
    [HttpGet("verify/{code}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CertificateVerificationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificateVerificationDto>> Verify(string code, CancellationToken ct) =>
        Ok(await _certificates.VerifyAsync(code, ct));

    /// <summary>
    /// Sertifikatın A4 ölçüsündə sənədini endirir. Doğrulama kimi publikdir —
    /// işəgötürən kodu bilirsə sertifikatı yükləyib yoxlaya bilər.
    /// </summary>
    /// <param name="code">Sertifikat kodu, məs. <c>MIM-2026-4417</c>.</param>
    /// <param name="format"><c>png</c> (default) və ya <c>pdf</c>.</param>
    [HttpGet("{code}/download")]
    [AllowAnonymous]
    [Produces("image/png", "application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        string code, [FromQuery] string format = "png", CancellationToken ct = default)
    {
        var documentFormat = format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? CertificateDocumentFormat.Pdf
            : CertificateDocumentFormat.Png;

        var document = await _certificates.RenderAsync(code, documentFormat, ct);

        return File(document.Content, document.ContentType, document.FileName);
    }

    /// <summary>Təlim üçün əl ilə sertifikat verir. Yalnız Admin.</summary>
    [HttpPost("issue")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(CertificateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CertificateDto>> Issue(IssueCertificateRequest request, CancellationToken ct)
    {
        var certificate = await _certificates.IssueAsync(request, ct);
        return Created($"/api/v1/certificates/verify/{certificate.Code}", certificate);
    }
}
