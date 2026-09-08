using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Quizzes;
using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Constants;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Api.Controllers;

/// <summary>Resurs Bankı — dərs materiallarının yüklənməsi, axtarışı və endirilməsi.</summary>
[ApiController]
[Route("api/v1/resources")]
[Produces("application/json")]
public sealed class ResourcesController : ControllerBase
{
    private readonly IResourceService _resources;
    private readonly IQuizService _quizzes;

    public ResourcesController(IResourceService resources, IQuizService quizzes)
    {
        _resources = resources;
        _quizzes = quizzes;
    }

    /// <summary>Resursların filtrlənmiş siyahısı. Default olaraq yalnız təsdiqlənmiş resurslar qayıdır.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ResourceDto>>> Get(
        [FromQuery] string? subject,
        [FromQuery] int? grade,
        [FromQuery] ResourceType? type,
        [FromQuery] ResourceStatus? status,
        [FromQuery] bool? isPaid,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ResourceQuery
        {
            Subject = subject,
            Grade = grade,
            Type = type,
            Status = status,
            IsPaid = isPaid,
            Search = search,
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _resources.GetAsync(query, ct));
    }

    /// <summary>Resursun detalları.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResourceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResourceDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _resources.GetByIdAsync(id, ct));

    /// <summary>
    /// Yeni resurs yükləyir (multipart/form-data). Resurs moderasiya növbəsinə (Pending) düşür.
    /// İcazə verilən formatlar: PDF, DOCX, PPTX; maksimum 25 MB.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.AuthorRoles)]
    [RequestSizeLimit(26_214_400)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ResourceDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResourceDetailDto>> Create(
        [FromForm] CreateResourceRequest request,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Fayl göndərilməlidir.",
                Errors = new Dictionary<string, string[]> { ["file"] = new[] { "Fayl seçilməyib." } },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        await using var stream = file.OpenReadStream();
        var upload = new ResourceFileUpload(stream, file.FileName, file.Length, file.ContentType);

        var created = await _resources.CreateAsync(request, upload, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Fayl yükləmədən link əsaslı resurs yaradır: video dərs (YouTube/Vimeo) və ya
    /// başqa saytda hazırlanmış material. Digər resurslar kimi moderasiyaya düşür.
    /// <c>ExternalLink</c> tipi yalnız pulsuz ola bilər, <c>Video</c> ödənişli də ola bilər.
    /// </summary>
    [HttpPost("link")]
    [Authorize(Roles = AppRoles.AuthorRoles)]
    [ProducesResponseType(typeof(ResourceDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResourceDetailDto>> CreateLink(
        CreateResourceLinkRequest request, CancellationToken ct)
    {
        var created = await _resources.CreateLinkAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Resursu endirir: endirmə sayını artırır və fayl linkini qaytarır.
    /// Link əsaslı resurslarda fayl yerinə xarici ünvan qayıdır (<c>isExternal: true</c>) —
    /// bu halda link endirilməməli, yeni tabda açılmalıdır.
    /// Ödənişli resurs üçün əvvəlcədən satın alınma tələb olunur.
    /// </summary>
    [HttpPost("{id:guid}/download")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResourceDownloadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResourceDownloadDto>> Download(Guid id, CancellationToken ct) =>
        Ok(await _resources.DownloadAsync(id, ct));

    /// <summary>Cari istifadəçinin yüklədiyi resurslar (bütün statuslar daxil).</summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ResourceDto>>> Mine(CancellationToken ct) =>
        Ok(await _resources.GetMineAsync(ct));

    /// <summary>Müəllifin ictimai profili və təsdiqlənmiş resursları.</summary>
    [HttpGet("author/{userId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthorProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthorProfileDto>> AuthorProfile(Guid userId, CancellationToken ct) =>
        Ok(await _resources.GetAuthorProfileAsync(userId, ct));

    /// <summary>Resursa bağlı imtahanın metadata-sı (suallar daxil deyil).</summary>
    [HttpGet("{resourceId:guid}/quiz")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuizDto>> GetQuiz(Guid resourceId, CancellationToken ct) =>
        Ok(await _quizzes.GetByResourceAsync(resourceId, ct));

    /// <summary>
    /// Resurs üçün imtahan yaradır və ya sual əlavə edir. Yalnız resursun müəllifi və ya admin.
    /// <c>mode</c>: "append" (default) mövcud suallara əlavə edir, "replace" hamısını əvəz edir.
    /// </summary>
    [HttpPost("{resourceId:guid}/quiz")]
    [Authorize]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuizDto>> UpsertQuiz(
        Guid resourceId, CreateQuizRequest request, CancellationToken ct) =>
        Ok(await _quizzes.CreateOrUpdateAsync(resourceId, request, ct));
}
