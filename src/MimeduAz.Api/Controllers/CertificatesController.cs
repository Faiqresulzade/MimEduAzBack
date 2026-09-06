using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
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
