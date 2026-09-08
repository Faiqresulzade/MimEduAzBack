using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Contracts.Certificates;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class CertificateService : ICertificateService
{
    private static readonly CultureInfo AzCulture = CultureInfo.InvariantCulture;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICertificateDocumentService _documents;
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICertificateDocumentService documents,
        ILogger<CertificateService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _documents = documents;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CertificateDto>> GetMineAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var certificates = await _db.Certificates
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Training)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync(ct);

        return certificates.Select(c => c.ToDto()).ToList();
    }

    public async Task<CertificateVerificationDto> VerifyAsync(string code, CancellationToken ct)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();

        var certificate = await _db.Certificates
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Code.ToUpper() == normalized, ct);

        if (certificate is null)
        {
            return new CertificateVerificationDto(false, normalized, null, null, null);
        }

        return new CertificateVerificationDto(
            true,
            certificate.Code,
            certificate.User?.FullName,
            certificate.Description,
            certificate.IssuedAt);
    }

    public async Task<CertificateDto> IssueAsync(IssueCertificateRequest request, CancellationToken ct)
    {
        var fullName = request.UserFullName.Trim();

        var matches = await _db.Users
            .Where(u => u.FullName.ToLower() == fullName.ToLower())
            .ToListAsync(ct);

        if (matches.Count == 0)
        {
            throw new NotFoundException($"«{fullName}» adlı istifadəçi tapılmadı.");
        }

        if (matches.Count > 1)
        {
            throw new ConflictException(
                $"«{fullName}» adı ilə {matches.Count} istifadəçi var. Sertifikatı e-poçt üzrə seçilmiş hesaba verin.");
        }

        var user = matches[0];

        var training = await _db.Trainings
            .FirstOrDefaultAsync(t => t.Id == request.TrainingId, ct)
            ?? throw NotFoundException.For("Təlim", request.TrainingId);

        var issuedAt = DateTime.UtcNow;

        var certificate = new Certificate
        {
            Code = await GenerateCertificateCodeAsync(issuedAt, ct),
            UserId = user.Id,
            TrainingId = training.Id,
            Description = BuildTrainingDescription(user.FullName, training.Name, training.DurationHours, issuedAt),
            IssuedAt = issuedAt
        };

        _db.Certificates.Add(certificate);

        // Sertifikat verilirsə təlim tamamlanmış sayılır.
        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == user.Id && e.TrainingId == training.Id, ct);

        if (enrollment is not null)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.ProgressPercent = 100;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sertifikat verildi. CertificateId: {CertificateId}", certificate.Id);

        certificate.User = user;
        certificate.Training = training;
        return certificate.ToDto();
    }

    public async Task<CertificateDto> IssueForQuizAttemptAsync(Guid attemptId, CancellationToken ct)
    {
        var existing = await _db.Certificates
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.ResourceQuizAttemptId == attemptId, ct);

        if (existing is not null)
        {
            return existing.ToDto();
        }

        var attempt = await _db.QuizAttempts
            .Include(a => a.User)
            .Include(a => a.Quiz)!
                .ThenInclude(q => q!.Resource)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct)
            ?? throw NotFoundException.For("İmtahan cəhdi", attemptId);

        if (!attempt.Passed)
        {
            throw new BadRequestException("Sertifikat yalnız uğurlu imtahan cəhdi üçün verilir.");
        }

        var issuedAt = DateTime.UtcNow;
        var resourceName = attempt.Quiz?.Resource?.Name ?? "Resurs";

        var certificate = new Certificate
        {
            Code = await GenerateCertificateCodeAsync(issuedAt, ct),
            UserId = attempt.UserId,
            ResourceQuizAttemptId = attempt.Id,
            Description = BuildQuizDescription(
                attempt.User?.FullName ?? string.Empty, resourceName, attempt.ScorePercent, issuedAt),
            IssuedAt = issuedAt
        };

        _db.Certificates.Add(certificate);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "İmtahan sertifikatı verildi. CertificateId: {CertificateId}, AttemptId: {AttemptId}",
            certificate.Id, attempt.Id);

        certificate.User = attempt.User;
        return certificate.ToDto();
    }

    public async Task<CertificateDto> IssueForTrainingCompletionAsync(Guid enrollmentId, CancellationToken ct)
    {
        var enrollment = await _db.Enrollments
            .Include(e => e.User)
            .Include(e => e.Training)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct)
            ?? throw NotFoundException.For("Təlim qeydiyyatı", enrollmentId);

        if (enrollment.Status != EnrollmentStatus.Completed)
        {
            throw new BadRequestException("Sertifikat yalnız tamamlanmış təlim üçün verilir.");
        }

        // Eyni istifadəçi+təlim üçün ikinci sertifikat yaradılmır.
        var existing = await _db.Certificates
            .Include(c => c.User)
            .Include(c => c.Training)
            .FirstOrDefaultAsync(
                c => c.UserId == enrollment.UserId && c.TrainingId == enrollment.TrainingId, ct);

        if (existing is not null)
        {
            return existing.ToDto();
        }

        var issuedAt = DateTime.UtcNow;
        var training = enrollment.Training;

        var certificate = new Certificate
        {
            Code = await GenerateCertificateCodeAsync(issuedAt, ct),
            UserId = enrollment.UserId,
            TrainingId = enrollment.TrainingId,
            Description = BuildTrainingDescription(
                enrollment.User?.FullName ?? string.Empty,
                training?.Name ?? "Təlim",
                training?.DurationHours ?? 0,
                issuedAt),
            IssuedAt = issuedAt
        };

        _db.Certificates.Add(certificate);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Təlim sertifikatı avtomatik verildi. CertificateId: {CertificateId}, EnrollmentId: {EnrollmentId}",
            certificate.Id, enrollmentId);

        certificate.User = enrollment.User;
        certificate.Training = training;
        return certificate.ToDto();
    }

    public async Task<CertificateDocument> RenderAsync(
        string code, CertificateDocumentFormat format, CancellationToken ct)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();

        var certificate = await _db.Certificates
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Training)
            .FirstOrDefaultAsync(c => c.Code.ToUpper() == normalized, ct)
            ?? throw new NotFoundException($"«{normalized}» kodlu sertifikat tapılmadı.");

        return _documents.Render(certificate, format);
    }

    private static string BuildTrainingDescription(string fullName, string trainingName, int hours, DateTime issuedAt) =>
        $"{fullName} · «{trainingName}» · {hours} saat · {issuedAt.ToString("dd.MM.yyyy", AzCulture)}";

    private static string BuildQuizDescription(string fullName, string resourceName, int score, DateTime issuedAt) =>
        $"{fullName} · «{resourceName}» imtahanı · {score}% · {issuedAt.ToString("dd.MM.yyyy", AzCulture)}";

    private Task<string> GenerateCertificateCodeAsync(DateTime issuedAt, CancellationToken ct) =>
        CodeFactory.UniqueAsync(
            () => CodeFactory.NewCertificateCode(issuedAt),
            (code, token) => _db.Certificates.AnyAsync(c => c.Code == code, token),
            () => CodeFactory.NewCertificateCodeLong(issuedAt),
            ct);
}
