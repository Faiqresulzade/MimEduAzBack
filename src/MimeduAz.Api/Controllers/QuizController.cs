using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Quizzes;

namespace MimeduAz.Api.Controllers;

/// <summary>İmtahanın keçilməsi.</summary>
[ApiController]
[Route("api/v1/quiz")]
[Authorize]
[Produces("application/json")]
public sealed class QuizController : ControllerBase
{
    private readonly IQuizService _quizzes;

    public QuizController(IQuizService quizzes) => _quizzes = quizzes;

    /// <summary>
    /// İmtahana başlamaq üçün sualları qaytarır.
    /// Cavabın düzgün variantı (correctOptionIndex) heç vaxt göndərilmir.
    /// </summary>
    [HttpGet("{quizId:guid}/questions")]
    [ProducesResponseType(typeof(IReadOnlyList<QuizQuestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<QuizQuestionDto>>> GetQuestions(Guid quizId, CancellationToken ct) =>
        Ok(await _quizzes.GetQuestionsAsync(quizId, ct));

    /// <summary>
    /// Cavabları göndərir və nəticəni qaytarır. Keçid balı toplanarsa
    /// avtomatik sertifikat verilir və kodu cavaba əlavə olunur.
    /// </summary>
    [HttpPost("{quizId:guid}/submit")]
    [ProducesResponseType(typeof(QuizResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuizResultDto>> Submit(
        Guid quizId, SubmitQuizRequest request, CancellationToken ct) =>
        Ok(await _quizzes.SubmitAsync(quizId, request, ct));
}
