using MimeduAz.Contracts.Ksq;

namespace MimeduAz.Application.Common.Interfaces;

/// <summary>
/// Cavab kartının şəklini vision model ilə oxuyur. Şəkil yalnız yaddaşda işlənir —
/// diskə, bazaya və ya loga yazılmır.
/// </summary>
public interface IKsqCardReader
{
    Task<KsqScanResultDto> ReadAsync(
        byte[] image,
        string contentType,
        int closedCount,
        int openCount,
        CancellationToken ct);
}
