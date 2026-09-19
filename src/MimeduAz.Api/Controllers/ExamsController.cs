using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Exams;
using MimeduAz.Domain.Constants;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Api.Controllers;

/// <summary>Sınaqlar — müəllimlər yaradır və satır, iştirakçılar vaxt limiti ilə verir.</summary>
[ApiController]
[Route("api/v1/exams")]
[Produces("application/json")]
public sealed class ExamsController : ControllerBase
{
    private readonly IExamService _exams;

    public ExamsController(IExamService exams) => _exams = exams;

    /// <summary>Sınaq kataloqu. Publik — default olaraq yalnız təsdiqlənmiş sınaqlar.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ExamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ExamDto>>> Get(
        [FromQuery] string? subject,
        [FromQuery] int? grade,
        [FromQuery] bool? isPaid,
        [FromQuery] ExamStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ExamQuery
        {
            Subject = subject,
            Grade = grade,
            IsPaid = isPaid,
            Status = status,
            Search = search,
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _exams.GetAsync(query, ct));
    }

    /// <summary>
    /// Sınağın detalı: bölmələr və hər bölmədəki sual sayı.
    /// Sualların özü burada YOXDUR — onlar yalnız <c>POST /exams/{id}/start</c> ilə açılır.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _exams.GetByIdAsync(id, ct));

    /// <summary>Yeni sınaq yaradır (bölmələr və suallarla). Moderasiyaya düşür.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.AuthorRoles)]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExamDetailDto>> Create(CreateExamRequest request, CancellationToken ct)
    {
        var created = await _exams.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Sınağın meta məlumatlarını yeniləyir. Suallara toxunmur.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.AuthorRoles)]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamDetailDto>> Update(
        Guid id, UpdateExamRequest request, CancellationToken ct) =>
        Ok(await _exams.UpdateAsync(id, request, ct));

    /// <summary>
    /// Sınağın bütün bölmə və suallarını əvəz edir. Kimsə sınağı veribsə 409 qaytarılır.
    /// Təsdiqlənmiş sınaq məzmunu dəyişəndə yenidən moderasiyaya düşür.
    /// </summary>
    [HttpPut("{id:guid}/sections")]
    [Authorize(Roles = AppRoles.AuthorRoles)]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamDetailDto>> SaveSections(
        Guid id, SaveExamSectionsRequest request, CancellationToken ct) =>
        Ok(await _exams.SaveSectionsAsync(id, request, ct));

    /// <summary>Sınağı silir. Kimsə sınağı veribsə 409 qaytarılır.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _exams.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Sual şəkli yükləyir (düstur, qrafik, cədvəl üçün). Cavabdakı <c>imagePath</c>
    /// dəyəri sual yaradılarkən <c>imagePath</c> sahəsinə yazılmalıdır.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = AppRoles.AuthorRoles)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ExamImageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExamImageDto>> UploadImage(
        Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Şəkil göndərilməlidir.",
                Errors = new Dictionary<string, string[]> { ["file"] = new[] { "Şəkil seçilməyib." } },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        await using var stream = file.OpenReadStream();
        var upload = new ExamImageUpload(stream, file.FileName, file.Length);

        return Ok(await _exams.UploadQuestionImageAsync(id, upload, ct));
    }

    /// <summary>Müəllimin yaratdığı sınaqlar (bütün statuslar daxil).</summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ExamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExamDto>>> Mine(CancellationToken ct) =>
        Ok(await _exams.GetMineAsync(ct));

    /// <summary>Cari istifadəçinin satın aldığı sınaqlar.</summary>
    [HttpGet("purchased")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ExamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExamDto>>> Purchased(CancellationToken ct) =>
        Ok(await _exams.GetPurchasedAsync(ct));

    /// <summary>
    /// Sınağı başladır və sualları qaytarır. Vaxt sayğacı server tərəfdə qeyd olunur —
    /// cavabdakı <c>expiresAt</c>/<c>remainingSeconds</c> ilə qurulmalıdır.
    /// Səhifə yenilənsə təkrar çağırış eyni cəhdi davam etdirir, vaxt sıfırlanmır.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [Authorize]
    [ProducesResponseType(typeof(ExamRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamRunDto>> Start(Guid id, CancellationToken ct) =>
        Ok(await _exams.StartAsync(id, ct));

    /// <summary>
    /// Cavabları təhvil verir və nəticəni qaytarır. Keçid balından yuxarı nəticədə
    /// sertifikat avtomatik verilir. Vaxt bitibsə 409 qaytarılır.
    /// </summary>
    [HttpPost("attempts/{attemptId:guid}/submit")]
    [Authorize]
    [ProducesResponseType(typeof(ExamResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExamResultDto>> Submit(
        Guid attemptId, SubmitExamRequest request, CancellationToken ct) =>
        Ok(await _exams.SubmitAsync(attemptId, request, ct));

    /// <summary>Tamamlanmış cəhdin nəticəsi — fənn üzrə bal və cavab açarı ilə.</summary>
    [HttpGet("attempts/{attemptId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ExamResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamResultDto>> Result(Guid attemptId, CancellationToken ct) =>
        Ok(await _exams.GetResultAsync(attemptId, ct));

    /// <summary>Cari istifadəçinin bütün sınaq cəhdləri.</summary>
    [HttpGet("attempts/mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ExamAttemptSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExamAttemptSummaryDto>>> MyAttempts(CancellationToken ct) =>
        Ok(await _exams.GetMyAttemptsAsync(ct));
}
