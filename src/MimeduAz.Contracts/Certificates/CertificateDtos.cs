namespace MimeduAz.Contracts.Certificates;

public sealed record CertificateDto(
    Guid Id,
    string Code,
    string HolderName,
    string Description,
    Guid? TrainingId,
    string? TrainingName,
    DateTime IssuedAt);

/// <summary>Publik doğrulama cavabı.</summary>
public sealed record CertificateVerificationDto(
    bool IsValid,
    string Code,
    string? HolderName,
    string? Description,
    DateTime? IssuedAt);

public sealed class IssueCertificateRequest
{
    /// <summary>Sertifikat veriləcək istifadəçinin tam adı (mövcud istifadəçilər arasından tapılır).</summary>
    public string UserFullName { get; set; } = string.Empty;
    public Guid TrainingId { get; set; }
}
