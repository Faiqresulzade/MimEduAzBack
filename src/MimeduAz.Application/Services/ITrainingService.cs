using MimeduAz.Contracts.Trainings;

namespace MimeduAz.Application.Services;

public interface ITrainingService
{
    Task<IReadOnlyList<TrainingDto>> GetAsync(CancellationToken ct);
    Task<TrainingDetailDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<TrainingDetailDto> CreateAsync(CreateTrainingRequest request, CancellationToken ct);
    /// <summary>Təlimin məlumatlarını yeniləyir. Yalnız Admin.</summary>
    Task<TrainingDetailDto> UpdateAsync(Guid id, UpdateTrainingRequest request, CancellationToken ct);

    /// <summary>
    /// Təlimi silir. Qeydiyyatı olan təlim silinmir - əks halda istifadəçilərin
    /// aldığı məzmun və sertifikat tarixçəsi arxasınca silinərdi.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<MyTrainingDto>> GetMineAsync(CancellationToken ct);

    /// <summary>
    /// Təlimin dərsləri və istifadəçinin irəliləyişi.
    /// Video linkləri yalnız təlimə yazılmış istifadəçiyə (və adminə) qaytarılır.
    /// </summary>
    Task<TrainingLessonsDto> GetLessonsAsync(Guid trainingId, CancellationToken ct);

    /// <summary>
    /// Dərsi tamamlanmış/tamamlanmamış işarələyir və irəliləyişi yenidən hesablayır.
    /// Bütün dərslər bitəndə təlim tamamlanmış sayılır və sertifikat avtomatik verilir.
    /// </summary>
    Task<LessonProgressDto> SetLessonCompletionAsync(
        Guid trainingId, Guid lessonId, bool isCompleted, CancellationToken ct);

    /// <summary>Təlimə dərs əlavə edir və ya hamısını əvəz edir. Yalnız Admin.</summary>
    Task<TrainingLessonsDto> SaveLessonsAsync(
        Guid trainingId, SaveTrainingLessonsRequest request, CancellationToken ct);
}
