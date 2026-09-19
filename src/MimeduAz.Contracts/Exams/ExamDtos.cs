using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Exams;

/// <summary>Sınaq kataloqundakı sətir. Suallar burada YOXDUR.</summary>
public sealed record ExamDto(
    Guid Id,
    string Name,
    string Subject,
    int? Grade,
    Guid AuthorId,
    string AuthorName,
    int DurationMinutes,
    int PassPercent,
    bool IsPaid,
    decimal Price,
    ExamStatus Status,
    int SectionCount,
    int QuestionCount,
    /// <summary>Neçə nəfər bu sınağı verib (tamamlanmış cəhd sayı).</summary>
    int AttemptCount,
    bool IsPurchased,
    /// <summary>Cari istifadəçi sınağa indi başlaya bilirmi (pulsuzdur / alıb / müəllifidir / admindir).</summary>
    bool CanAccess,
    DateTime CreatedAt,
    DateTime? ApprovedAt);

/// <summary>Sınağın detalı — bölmələr və hər bölmədəki sual sayı. Sualların özü YOXDUR.</summary>
public sealed record ExamDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Subject,
    int? Grade,
    Guid AuthorId,
    string AuthorName,
    int DurationMinutes,
    int PassPercent,
    bool IsPaid,
    decimal Price,
    ExamStatus Status,
    string? RejectionReason,
    IReadOnlyList<ExamSectionSummaryDto> Sections,
    int QuestionCount,
    int AttemptCount,
    bool IsPurchased,
    bool CanAccess,
    /// <summary>Cari istifadəçinin bu sınaq üzrə cəhdi (varsa) — "davam et" və ya "nəticəyə bax" üçün.</summary>
    ExamAttemptSummaryDto? MyAttempt,
    DateTime CreatedAt,
    DateTime? ApprovedAt);

public sealed record ExamSectionSummaryDto(
    Guid Id,
    int OrderIndex,
    string Subject,
    int QuestionCount);

// ---------------------------------------------------------------- Sınaq gedişi

/// <summary>Sınaq başladıldıqda qaytarılan paket: suallar + server tərəfli vaxt.</summary>
public sealed record ExamRunDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamName,
    int DurationMinutes,
    DateTime StartedAt,
    /// <summary>Cavabların qəbul olunduğu son an. Sayğac buna görə qurulmalıdır.</summary>
    DateTime ExpiresAt,
    /// <summary>Qalan saniyə — brauzerin saatı yanlışdırsa da düzgün başlanğıc nöqtəsi.</summary>
    int RemainingSeconds,
    IReadOnlyList<ExamRunSectionDto> Sections,
    int QuestionCount);

public sealed record ExamRunSectionDto(
    Guid Id,
    int OrderIndex,
    string Subject,
    IReadOnlyList<ExamRunQuestionDto> Questions);

/// <summary>Sınaq gedərkən göstərilən sual. Düzgün cavab BURADA YOXDUR.</summary>
public sealed record ExamRunQuestionDto(
    Guid Id,
    int OrderIndex,
    string QuestionText,
    /// <summary>Sual şəklinin publik URL-i (varsa). Düsturlu/qrafikli suallar üçün.</summary>
    string? ImageUrl,
    IReadOnlyList<string> Options);

public sealed class SubmitExamRequest
{
    public List<SubmitExamAnswerRequest> Answers { get; set; } = new();
}

public sealed class SubmitExamAnswerRequest
{
    public Guid QuestionId { get; set; }

    /// <summary>Seçilmiş variantın indeksi. Sual cavabsız buraxılıbsa null göndərin.</summary>
    public int? SelectedIndex { get; set; }
}

// ---------------------------------------------------------------- Nəticə

/// <summary>Cəhdin qısa xülasəsi (siyahılar üçün).</summary>
public sealed record ExamAttemptSummaryDto(
    Guid Id,
    Guid ExamId,
    string ExamName,
    ExamAttemptStatus Status,
    int ScorePercent,
    int CorrectCount,
    int QuestionCount,
    bool Passed,
    DateTime StartedAt,
    DateTime ExpiresAt,
    DateTime? SubmittedAt,
    string? CertificateCode);

/// <summary>Sınaq bitdikdən sonrakı tam nəticə: fənn üzrə bal + cavab açarı.</summary>
public sealed record ExamResultDto(
    Guid AttemptId,
    Guid ExamId,
    string ExamName,
    ExamAttemptStatus Status,
    int ScorePercent,
    int CorrectCount,
    int QuestionCount,
    int PassPercent,
    bool Passed,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    /// <summary>Sınağa sərf olunan vaxt (saniyə).</summary>
    int ElapsedSeconds,
    IReadOnlyList<ExamSectionResultDto> Sections,
    string? CertificateCode);

/// <summary>Bir fənn bölməsi üzrə nəticə.</summary>
public sealed record ExamSectionResultDto(
    Guid SectionId,
    string Subject,
    int CorrectCount,
    int QuestionCount,
    int ScorePercent,
    IReadOnlyList<ExamAnswerReviewDto> Questions);

/// <summary>Nəticə ekranındakı sual təhlili — burada düzgün cavab AÇIQ göndərilir.</summary>
public sealed record ExamAnswerReviewDto(
    Guid QuestionId,
    int OrderIndex,
    string QuestionText,
    string? ImageUrl,
    IReadOnlyList<string> Options,
    int CorrectOptionIndex,
    int? SelectedIndex,
    bool IsCorrect);

// ---------------------------------------------------------------- Yaratma / redaktə

public sealed class CreateExamRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int? Grade { get; set; }
    public int DurationMinutes { get; set; }
    public int PassPercent { get; set; } = 60;
    public bool IsPaid { get; set; }
    public decimal Price { get; set; }

    public List<CreateExamSectionRequest> Sections { get; set; } = new();
}

/// <summary>Sınağın meta məlumatları. Suallar ayrıca endpoint ilə idarə olunur.</summary>
public sealed class UpdateExamRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int? Grade { get; set; }
    public int DurationMinutes { get; set; }
    public int PassPercent { get; set; } = 60;
    public bool IsPaid { get; set; }
    public decimal Price { get; set; }
}

public sealed class CreateExamSectionRequest
{
    public string Subject { get; set; } = string.Empty;
    public List<CreateExamQuestionRequest> Questions { get; set; } = new();
}

public sealed class CreateExamQuestionRequest
{
    /// <summary>Sual mətni. Sual tamamilə şəkildən ibarətdirsə boş buraxıla bilər.</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// <c>POST /exams/{id}/images</c> cavabındakı <c>imagePath</c> dəyəri.
    /// Düsturlu/qrafikli suallar üçün.
    /// </summary>
    public string? ImagePath { get; set; }

    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; }
}

/// <summary>Sınağın bütün bölmə və suallarını əvəz edir.</summary>
public sealed class SaveExamSectionsRequest
{
    public List<CreateExamSectionRequest> Sections { get; set; } = new();
}

/// <summary>Sual şəkli yükləndikdən sonra qaytarılan yol.</summary>
public sealed record ExamImageDto(string ImagePath, string ImageUrl);

public sealed class RejectExamRequest
{
    public string? Reason { get; set; }
}
