using MimeduAz.Contracts.Certificates;

namespace MimeduAz.Application.Services;

public interface ICertificateService
{
    Task<IReadOnlyList<CertificateDto>> GetMineAsync(CancellationToken ct);

    /// <summary>Publik doğrulama — autentifikasiya tələb olunmur.</summary>
    Task<CertificateVerificationDto> VerifyAsync(string code, CancellationToken ct);

    /// <summary>Admin tərəfindən əl ilə təlim sertifikatı verilməsi.</summary>
    Task<CertificateDto> IssueAsync(IssueCertificateRequest request, CancellationToken ct);

    /// <summary>
    /// Uğurlu imtahan cəhdi üçün sertifikat verir. Eyni cəhd üçün təkrar çağırılarsa
    /// mövcud sertifikatı qaytarır (idempotent).
    /// </summary>
    Task<CertificateDto> IssueForQuizAttemptAsync(Guid attemptId, CancellationToken ct);
}
