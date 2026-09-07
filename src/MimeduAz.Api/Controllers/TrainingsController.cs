using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Trainings;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>Təlimlər — kataloq, dərslər və istifadəçinin irəliləyişi.</summary>
[ApiController]
[Route("api/v1/trainings")]
[Produces("application/json")]
public sealed class TrainingsController : ControllerBase
{
    private readonly ITrainingService _trainings;

    public TrainingsController(ITrainingService trainings) => _trainings = trainings;

    /// <summary>Bütün təlimlərin siyahısı (tutulmuş/qalan yerlər və dərs sayı ilə).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<TrainingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrainingDto>>> Get(CancellationToken ct) =>
        Ok(await _trainings.GetAsync(ct));

    /// <summary>
    /// Təlimin detalları və ictimai proqramı. Daxil olmuş istifadəçi üçün
    /// <c>isEnrolled</c> sahəsi də doldurulur.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TrainingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _trainings.GetByIdAsync(id, ct));

    /// <summary>Yeni təlim yaradır (dərslərlə birlikdə). Yalnız Admin.</summary>
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

    /// <summary>
    /// Təlimin dərsləri və irəliləyiş. Video linkləri yalnız bu təlimə
    /// yazılmış istifadəçiyə (və adminə) qaytarılır — əks halda 403.
    /// </summary>
    [HttpGet("{trainingId:guid}/lessons")]
    [Authorize]
    [ProducesResponseType(typeof(TrainingLessonsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingLessonsDto>> Lessons(Guid trainingId, CancellationToken ct) =>
        Ok(await _trainings.GetLessonsAsync(trainingId, ct));

    /// <summary>
    /// Dərsi tamamlanmış işarələyir. Bütün dərslər bitəndə təlim avtomatik
    /// tamamlanır və sertifikat verilir (kodu cavabda qayıdır).
    /// </summary>
    [HttpPost("{trainingId:guid}/lessons/{lessonId:guid}/complete")]
    [Authorize]
    [ProducesResponseType(typeof(LessonProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonProgressDto>> CompleteLesson(
        Guid trainingId, Guid lessonId, CancellationToken ct) =>
        Ok(await _trainings.SetLessonCompletionAsync(trainingId, lessonId, isCompleted: true, ct));

    /// <summary>Dərsin "tamamlandı" işarəsini geri götürür.</summary>
    [HttpDelete("{trainingId:guid}/lessons/{lessonId:guid}/complete")]
    [Authorize]
    [ProducesResponseType(typeof(LessonProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LessonProgressDto>> UncompleteLesson(
        Guid trainingId, Guid lessonId, CancellationToken ct) =>
        Ok(await _trainings.SetLessonCompletionAsync(trainingId, lessonId, isCompleted: false, ct));

    /// <summary>
    /// Təlimə dərs əlavə edir və ya hamısını əvəz edir. Yalnız Admin.
    /// <c>mode</c>: "append" (default) və ya "replace".
    /// </summary>
    [HttpPost("{trainingId:guid}/lessons")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(TrainingLessonsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TrainingLessonsDto>> SaveLessons(
        Guid trainingId, SaveTrainingLessonsRequest request, CancellationToken ct) =>
        Ok(await _trainings.SaveLessonsAsync(trainingId, request, ct));
}
