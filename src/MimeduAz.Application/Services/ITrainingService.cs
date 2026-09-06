using MimeduAz.Contracts.Trainings;

namespace MimeduAz.Application.Services;

public interface ITrainingService
{
    Task<IReadOnlyList<TrainingDto>> GetAsync(CancellationToken ct);
    Task<TrainingDetailDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<TrainingDetailDto> CreateAsync(CreateTrainingRequest request, CancellationToken ct);
    Task<IReadOnlyList<MyTrainingDto>> GetMineAsync(CancellationToken ct);
}
