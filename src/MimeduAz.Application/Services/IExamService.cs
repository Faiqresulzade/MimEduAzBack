using MimeduAz.Contracts.Common;
using MimeduAz.Contracts.Exams;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

/// <summary>Sınaq kataloqu filtri.</summary>
public sealed class ExamQuery
{
    public string? Subject { get; set; }
    public int? Grade { get; set; }
    public bool? IsPaid { get; set; }

    /// <summary>Yalnız Admin üçün işləyir; digərləri həmişə Approved görür.</summary>
    public ExamStatus? Status { get; set; }

    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Sual şəkli yükləmə girişi.</summary>
public sealed record ExamImageUpload(Stream Content, string FileName, long Length);

public interface IExamService
{
    Task<PagedResult<ExamDto>> GetAsync(ExamQuery query, CancellationToken ct);
    Task<ExamDetailDto> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Sınağı bölmə və sualları ilə birlikdə yaradır. Moderasiyaya (Pending) düşür.</summary>
    Task<ExamDetailDto> CreateAsync(CreateExamRequest request, CancellationToken ct);

    /// <summary>Sınağın meta məlumatlarını yeniləyir (suallara toxunmur).</summary>
    Task<ExamDetailDto> UpdateAsync(Guid id, UpdateExamRequest request, CancellationToken ct);

    /// <summary>
    /// Bütün bölmə və sualları əvəz edir. Cəhd varsa icazə verilmir —
    /// keçmiş nəticələr etibarsız olardı. Təsdiqlənmiş sınaq yenidən moderasiyaya düşür.
    /// </summary>
    Task<ExamDetailDto> SaveSectionsAsync(Guid id, SaveExamSectionsRequest request, CancellationToken ct);

    /// <summary>Sınağı silir. Cəhd varsa 409 qaytarılır.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Düsturlu/qrafikli suallar üçün şəkil yükləyir və nisbi yolu qaytarır.</summary>
    Task<ExamImageDto> UploadQuestionImageAsync(Guid examId, ExamImageUpload image, CancellationToken ct);

    Task<IReadOnlyList<ExamDto>> GetMineAsync(CancellationToken ct);

    /// <summary>Cari istifadəçinin satın aldığı sınaqlar.</summary>
    Task<IReadOnlyList<ExamDto>> GetPurchasedAsync(CancellationToken ct);

    /// <summary>
    /// Sınağı başladır: cəhd yaradır, vaxt sayğacını server tərəfdə qeyd edir
    /// və sualları düzgün cavab olmadan qaytarır. Təkrar çağırışda mövcud cəhd davam edir.
    /// </summary>
    Task<ExamRunDto> StartAsync(Guid examId, CancellationToken ct);

    /// <summary>
    /// Cavabları qiymətləndirir. Vaxt bitibsə cəhd Expired olur və sertifikat verilmir.
    /// Keçid balından yuxarı nəticədə sertifikat avtomatik verilir.
    /// </summary>
    Task<ExamResultDto> SubmitAsync(Guid attemptId, SubmitExamRequest request, CancellationToken ct);

    /// <summary>Tamamlanmış cəhdin nəticəsi — fənn üzrə bal və cavab açarı ilə.</summary>
    Task<ExamResultDto> GetResultAsync(Guid attemptId, CancellationToken ct);

    Task<IReadOnlyList<ExamAttemptSummaryDto>> GetMyAttemptsAsync(CancellationToken ct);
}
