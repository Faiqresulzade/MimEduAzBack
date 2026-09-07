using MimeduAz.Contracts.Trainings;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Mappings;

public static class TrainingMappings
{
    public static TrainingDto ToDto(this Training t, int seatsTaken, int lessonCount) => new(
        t.Id,
        t.Name,
        t.Format,
        t.Description,
        t.Price,
        t.DurationHours,
        t.MetaLabel,
        t.SeatLimit,
        seatsTaken,
        t.SeatLimit.HasValue ? Math.Max(0, t.SeatLimit.Value - seatsTaken) : null,
        lessonCount,
        t.CreatedAt);

    public static TrainingDetailDto ToDetailDto(
        this Training t, int seatsTaken, int lessonCount, bool isEnrolled) => new(
        t.Id,
        t.Name,
        t.Format,
        t.Description,
        t.Price,
        t.DurationHours,
        t.MetaLabel,
        t.SeatLimit,
        seatsTaken,
        t.SeatLimit.HasValue ? Math.Max(0, t.SeatLimit.Value - seatsTaken) : null,
        t.SyllabusItems
            .OrderBy(s => s.OrderIndex)
            .Select(s => new SyllabusItemDto(s.Id, s.OrderIndex, s.Text))
            .ToList(),
        lessonCount,
        isEnrolled,
        t.CreatedAt);
}
