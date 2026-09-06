using Microsoft.EntityFrameworkCore;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Contracts.Trainings;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class TrainingService : ITrainingService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TrainingService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TrainingDto>> GetAsync(CancellationToken ct)
    {
        var trainings = await _db.Trainings
            .AsNoTracking()
            .OrderBy(t => t.Format)
            .ThenBy(t => t.Name)
            .Select(t => new { Training = t, SeatsTaken = t.Enrollments.Count })
            .ToListAsync(ct);

        return trainings.Select(x => x.Training.ToDto(x.SeatsTaken)).ToList();
    }

    public async Task<TrainingDetailDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var training = await _db.Trainings
            .AsNoTracking()
            .Include(t => t.SyllabusItems)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw NotFoundException.For("Təlim", id);

        var seatsTaken = await _db.Enrollments.CountAsync(e => e.TrainingId == id, ct);
        return training.ToDetailDto(seatsTaken);
    }

    public async Task<TrainingDetailDto> CreateAsync(CreateTrainingRequest request, CancellationToken ct)
    {
        var training = new Training
        {
            Name = request.Name.Trim(),
            Format = request.Format,
            Description = request.Description.Trim(),
            Price = request.Price,
            DurationHours = request.DurationHours,
            MetaLabel = request.MetaLabel.Trim(),
            // Yer limiti yalnız canlı təlimlərdə mənalıdır; onlayn/video limitsizdir.
            SeatLimit = request.Format == TrainingFormat.Live ? request.SeatLimit : null,
            CreatedAt = DateTime.UtcNow
        };

        var order = 0;
        foreach (var text in request.Syllabus.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            training.SyllabusItems.Add(new TrainingSyllabusItem
            {
                TrainingId = training.Id,
                OrderIndex = order++,
                Text = text.Trim()
            });
        }

        _db.Trainings.Add(training);
        await _db.SaveChangesAsync(ct);

        return training.ToDetailDto(seatsTaken: 0);
    }

    public async Task<IReadOnlyList<MyTrainingDto>> GetMineAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var enrollments = await _db.Enrollments
            .AsNoTracking()
            .Include(e => e.Training)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync(ct);

        var trainingIds = enrollments.Select(e => e.TrainingId).ToList();

        // Təlim üzrə verilmiş sertifikatları bir sorğu ilə götürüb yaddaşda uyğunlaşdırırıq.
        var certificates = await _db.Certificates
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.TrainingId != null && trainingIds.Contains(c.TrainingId.Value))
            .ToListAsync(ct);

        var certificateByTraining = certificates
            .GroupBy(c => c.TrainingId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.IssuedAt).First().Code);

        return enrollments.Select(e => new MyTrainingDto(
            e.Id,
            e.TrainingId,
            e.Training?.Name ?? string.Empty,
            e.Training?.Format ?? TrainingFormat.Online,
            e.Training?.MetaLabel ?? string.Empty,
            e.Training?.DurationHours ?? 0,
            e.ProgressPercent,
            e.Status,
            e.EnrolledAt,
            certificateByTraining.GetValueOrDefault(e.TrainingId))).ToList();
    }
}
