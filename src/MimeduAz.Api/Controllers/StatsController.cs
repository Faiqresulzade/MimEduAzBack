using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Domain.Constants;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Api.Controllers;

/// <summary>Ana səhifədəki göstəricilər.</summary>
public sealed record PlatformStatsDto(int ResourceCount, int TeacherCount, int TrainingCount, int TotalDownloads);

/// <summary>
/// Ana səhifənin ictimai statistikası. Rəqəmlər birbaşa bazadan sayılır —
/// heç bir "baza rəqəm" əlavə edilmir (bank yoxlaması və istehlakçı hüquqları).
/// </summary>
[ApiController]
[Route("api/v1/stats")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class StatsController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public StatsController(IApplicationDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(PlatformStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformStatsDto>> Get(CancellationToken ct)
    {
        var approved = _db.Resources.Where(r => r.Status == ResourceStatus.Approved);

        var teacherRoleId = await _db.Roles
            .Where(r => r.Name == AppRoles.Teacher)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);

        return Ok(new PlatformStatsDto(
            await approved.CountAsync(ct),
            await _db.UserRoles.CountAsync(ur => ur.RoleId == teacherRoleId, ct),
            await _db.Trainings.CountAsync(ct),
            await approved.SumAsync(r => r.Downloads, ct)));
    }
}
