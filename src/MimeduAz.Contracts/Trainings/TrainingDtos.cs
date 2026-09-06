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
    DateTime CreatedAt);

public sealed class CreateTrainingRequest
{
    public string Name { get; set; } = string.Empty;
    public TrainingFormat Format { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationHours { get; set; }
    public string MetaLabel { get; set; } = string.Empty;
    public int? SeatLimit { get; set; }
    public List<string> Syllabus { get; set; } = new();
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
    EnrollmentStatus Status,
    DateTime EnrolledAt,
    string? CertificateCode);
