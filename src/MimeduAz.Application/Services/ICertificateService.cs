using MimeduAz.Application.Common.Interfaces;
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

    /// <summary>
    /// Təlimin bütün dərsləri tamamlananda sertifikat verir. Eyni istifadəçi+təlim
    /// üçün təkrar çağırılarsa mövcud sertifikatı qaytarır (idempotent).
    /// </summary>
    Task<CertificateDto> IssueForTrainingCompletionAsync(Guid enrollmentId, CancellationToken ct);

    /// <summary>
    /// Sertifikatın endirilə bilən sənədini (A4 PNG və ya PDF) qaytarır.
    /// Doğrulama kimi publikdir - işəgötürən sertifikatı yükləyib yoxlaya bilsin.
    /// </summary>
    Task<CertificateDocument> RenderAsync(string code, CertificateDocumentFormat format, CancellationToken ct);
}
