using MimeduAz.Domain.Enums;

namespace MimeduAz.Contracts.Trainings;

public sealed record SyllabusItemDto(Guid Id, int OrderIndex, string Text);

public sealed record TrainingDto(
    Guid Id,
    string Name,
    TrainingFormat Format,
    string Description,
    decimal Price,
    int DurationHours,
    string MetaLabel,
    int? SeatLimit,
    int SeatsTaken,
    int? SeatsLeft,
    int LessonCount,
    DateTime CreatedAt);

public sealed record TrainingDetailDto(
    Guid Id,
    string Name,
    TrainingFormat Format,
    string Description,
    decimal Price,
    int DurationHours,
    string MetaLabel,
    int? SeatLimit,
    int SeatsTaken,
    int? SeatsLeft,
    IReadOnlyList<SyllabusItemDto> Syllabus,
    int LessonCount,
    /// <summary>Cari istifadəçi bu təlimə yazılıbmı — frontend "Dərslərə keç" düyməsi üçün.</summary>
    bool IsEnrolled,
    DateTime CreatedAt);

/// <summary>
/// Təlimin bir dərsi. <see cref="VideoUrl"/> yalnız təlimə yazılmış istifadəçiyə
/// (və adminə) doldurulur — əks halda null qalır.
/// </summary>
public sealed record TrainingLessonDto(
    Guid Id,
    int OrderIndex,
    string Title,
    string Description,
    string? VideoUrl,
    int? DurationMinutes,
    bool IsCompleted,
    DateTime? CompletedAt);

/// <summary>Təlimin dərs siyahısı + istifadəçinin irəliləyişi.</summary>
public sealed record TrainingLessonsDto(
    Guid TrainingId,
    string TrainingName,
    IReadOnlyList<TrainingLessonDto> Lessons,
    int CompletedLessonCount,
    int TotalLessonCount,
    int ProgressPercent,
    EnrollmentStatus Status,
    string? CertificateCode);

/// <summary>Dərs tamamlandıqdan sonra qayıdan yenilənmiş irəliləyiş.</summary>
public sealed record LessonProgressDto(
    Guid TrainingId,
    Guid LessonId,
    bool IsCompleted,
    int CompletedLessonCount,
    int TotalLessonCount,
    int ProgressPercent,
    EnrollmentStatus Status,
    /// <summary>Təlim bu addımla 100% tamamlanıbsa avtomatik verilən sertifikatın kodu.</summary>
    string? CertificateCode);

public sealed class CreateTrainingLessonRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public int? DurationMinutes { get; set; }
}

public sealed class CreateTrainingRequest
{
    public string Name { get; set; } = string.Empty;
    public TrainingFormat Format { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationHours { get; set; }
    public string MetaLabel { get; set; } = string.Empty;
    public int? SeatLimit { get; set; }

    /// <summary>İctimai proqram maddələri. Boş buraxılsa dərs başlıqlarından doldurulur.</summary>
    public List<string> Syllabus { get; set; } = new();

    /// <summary>Təlimin dərsləri (video məzmunu). Sonradan da əlavə oluna bilər.</summary>
    public List<CreateTrainingLessonRequest> Lessons { get; set; } = new();
}

/// <summary>Mövcud təlimə dərs əlavə etmə. <see cref="Mode"/> = "append" | "replace".</summary>
public sealed class SaveTrainingLessonsRequest
{
    public string Mode { get; set; } = "append";
    public List<CreateTrainingLessonRequest> Lessons { get; set; } = new();
}

/// <summary>"Mənim təlimlərim" siyahısı — enrollment + təlim məlumatı birlikdə.</summary>
public sealed record MyTrainingDto(
    Guid EnrollmentId,
    Guid TrainingId,
    string Name,
    TrainingFormat Format,
    string MetaLabel,
    int DurationHours,
    int ProgressPercent,
    int CompletedLessonCount,
    int TotalLessonCount,
    EnrollmentStatus Status,
    DateTime EnrolledAt,
    DateTime? CompletedAt,
    string? CertificateCode);
