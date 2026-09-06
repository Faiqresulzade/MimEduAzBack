using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Trainings;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>Təlimlər — kataloq və istifadəçinin yazıldığı təlimlər.</summary>
[ApiController]
[Route("api/v1/trainings")]
[Produces("application/json")]
public sealed class TrainingsController : ControllerBase
{
    private readonly ITrainingService _trainings;

    public TrainingsController(ITrainingService trainings) => _trainings = trainings;

    /// <summary>Bütün təlimlərin siyahısı (tutulmuş və qalan yerlərlə birlikdə).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<TrainingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrainingDto>>> Get(CancellationToken ct) =>
        Ok(await _trainings.GetAsync(ct));

    /// <summary>Təlimin detalları və proqramı.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TrainingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _trainings.GetByIdAsync(id, ct));

    /// <summary>Yeni təlim yaradır. Yalnız Admin.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(TrainingDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TrainingDetailDto>> Create(CreateTrainingRequest request, CancellationToken ct)
    {
        var created = await _trainings.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Cari istifadəçinin yazıldığı təlimlər (irəliləyiş və sertifikat kodu ilə).</summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<MyTrainingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyTrainingDto>>> Mine(CancellationToken ct) =>
        Ok(await _trainings.GetMineAsync(ct));
}
