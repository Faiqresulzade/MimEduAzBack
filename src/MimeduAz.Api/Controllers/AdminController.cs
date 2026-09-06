using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Admin;
using MimeduAz.Contracts.Common;
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

    /// <summary>Moderasiya növbəsi — təsdiq gözləyən resurslar.</summary>
    [HttpGet("resources/pending")]
    [ProducesResponseType(typeof(IReadOnlyList<ResourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ResourceDto>>> PendingResources(CancellationToken ct) =>
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
