using MimeduAz.Contracts.Quizzes;

namespace MimeduAz.Application.Services;

public interface IQuizService
{
    /// <summary>Resursun quiz metadata-sı (suallar daxil deyil).</summary>
    Task<QuizDto> GetByResourceAsync(Guid resourceId, CancellationToken ct);

    /// <summary>Quiz yaradır və ya suallar əlavə edir/əvəz edir. Yalnız resursun müəllifi və ya admin.</summary>
    Task<QuizDto> CreateOrUpdateAsync(Guid resourceId, CreateQuizRequest request, CancellationToken ct);

    /// <summary>İmtahana başlamaq üçün suallar. Düzgün cavabın indeksi cavabda YOXDUR.</summary>
    Task<IReadOnlyList<QuizQuestionDto>> GetQuestionsAsync(Guid quizId, CancellationToken ct);

    /// <summary>Cavabları qiymətləndirir; keçid balı toplanarsa avtomatik sertifikat verir.</summary>
    Task<QuizResultDto> SubmitAsync(Guid quizId, SubmitQuizRequest request, CancellationToken ct);
}
