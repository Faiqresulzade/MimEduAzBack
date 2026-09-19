using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Admin;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Exams;
using MimeduAz.Contracts.Orders;
using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>Admin paneli — moderasiya, istifadəçilər və satış statistikası.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = AppRoles.Admin)]
[Produces("application/json")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    /// <summary>
    /// Admin panelinin icmalı: sorğu və xəta sayları, məzmun, təlim və satış göstəriciləri.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardDto>> Dashboard(CancellationToken ct) =>
        Ok(await _admin.GetDashboardAsync(ct));

    /// <summary>
    /// Moderasiya növbəsi — təsdiq gözləyən resurslar. Cavabda tam detal gəlir:
    /// video/xarici link və fayl adı da daxil, moderator materialı görmədən qərar verməsin deyə.
    /// </summary>
    [HttpGet("resources/pending")]
    [ProducesResponseType(typeof(IReadOnlyList<ResourceDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ResourceDetailDto>>> PendingResources(CancellationToken ct) =>
        Ok(await _admin.GetPendingResourcesAsync(ct));

    /// <summary>Resursu təsdiqləyir — Resurs Bankında ictimai görünür.</summary>
    [HttpPost("resources/{id:guid}/approve")]
    [ProducesResponseType(typeof(ResourceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResourceDetailDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await _admin.ApproveResourceAsync(id, ct));

    /// <summary>Resursu rədd edir (opsional səbəblə).</summary>
    [HttpPost("resources/{id:guid}/reject")]
    [ProducesResponseType(typeof(ResourceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResourceDetailDto>> Reject(
        Guid id, RejectResourceRequest? request, CancellationToken ct) =>
        Ok(await _admin.RejectResourceAsync(id, request ?? new RejectResourceRequest(), ct));

    /// <summary>Moderasiya növbəsi — təsdiq gözləyən sınaqlar (bölmə və suallarla).</summary>
    [HttpGet("exams/pending")]
    [ProducesResponseType(typeof(IReadOnlyList<ExamDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExamDetailDto>>> PendingExams(CancellationToken ct) =>
        Ok(await _admin.GetPendingExamsAsync(ct));

    /// <summary>Sınağı təsdiqləyir — kataloqda görünür və satışa çıxır.</summary>
    [HttpPost("exams/{id:guid}/approve")]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamDetailDto>> ApproveExam(Guid id, CancellationToken ct) =>
        Ok(await _admin.ApproveExamAsync(id, ct));

    /// <summary>Sınağı rədd edir (opsional səbəblə).</summary>
    [HttpPost("exams/{id:guid}/reject")]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExamDetailDto>> RejectExam(
        Guid id, RejectExamRequest? request, CancellationToken ct) =>
        Ok(await _admin.RejectExamAsync(id, request ?? new RejectExamRequest(), ct));

    /// <summary>Bütün istifadəçilər və onların statistikası.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> Users(CancellationToken ct) =>
        Ok(await _admin.GetUsersAsync(ct));

    /// <summary>Satış xülasəsi: GMV, komissiya, müəllif payları və sifariş sayı.</summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(SalesSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SalesSummaryDto>> Sales(CancellationToken ct) =>
        Ok(await _admin.GetSalesAsync(ct));

    /// <summary>Bütün sifarişlər.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Orders(CancellationToken ct) =>
        Ok(await _admin.GetOrdersAsync(ct));

    /// <summary>
    /// HTTP audit log-u (bütün request/response qeydləri).
    /// Şifrə və token kimi həssas sahələr yazılarkən maskalanır.
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(PagedResult<RequestLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RequestLogDto>>> Logs(
        [FromQuery] string? method,
        [FromQuery] string? path,
        [FromQuery] int? statusCode,
        [FromQuery] Guid? userId,
        [FromQuery] bool? onlyErrors,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new RequestLogQuery
        {
            Method = method,
            Path = path,
            StatusCode = statusCode,
            UserId = userId,
            OnlyErrors = onlyErrors,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        };

        return Ok(await _admin.GetRequestLogsAsync(query, ct));
    }
}
