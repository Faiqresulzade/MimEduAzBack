using MimeduAz.Contracts.Ksq;

namespace MimeduAz.Application.Services;

/// <summary>KSQ cavab kartı yoxlaması. Bütün əməliyyatlar yalnız cari müəllimin məlumatına aiddir.</summary>
public interface IKsqService
{
    Task<IReadOnlyList<KsqTestDto>> GetTestsAsync(CancellationToken ct);
    Task<KsqTestDto> CreateTestAsync(SaveKsqTestRequest request, CancellationToken ct);
    Task<KsqTestDto> UpdateTestAsync(Guid id, SaveKsqTestRequest request, CancellationToken ct);

    /// <summary>Testi və onun bütün nəticələrini silir.</summary>
    Task DeleteTestAsync(Guid id, CancellationToken ct);

    Task<KsqResultListDto> GetResultsAsync(Guid? testId, string? sinif, CancellationToken ct);
    Task<KsqResultDto> SaveResultAsync(SaveKsqResultRequest request, CancellationToken ct);
    Task<IReadOnlyList<KsqResultDto>> SaveResultsAsync(SaveKsqResultsBulkRequest request, CancellationToken ct);
    Task DeleteResultAsync(Guid id, CancellationToken ct);

    /// <summary>Kart şəklini AI ilə oxuyur. Şəkil heç yerdə saxlanmır.</summary>
    Task<KsqScanResultDto> ScanAsync(
        Stream image, string contentType, long length, int closedCount, int openCount, CancellationToken ct);
}
