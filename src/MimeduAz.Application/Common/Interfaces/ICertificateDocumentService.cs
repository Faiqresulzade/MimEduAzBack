using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Common.Interfaces;

/// <summary>Endirilə bilən sertifikat sənədinin formatı.</summary>
public enum CertificateDocumentFormat
{
    Png = 0,
    Pdf = 1
}

/// <summary>Hazırlanmış sənəd: məzmun, MIME tipi və təklif olunan fayl adı.</summary>
public sealed record CertificateDocument(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Sertifikatın A4 ölçüsündə vizual sənədini yaradır (PNG və ya PDF).
/// Application qatı konkret render kitabxanasından asılı qalmır.
/// </summary>
public interface ICertificateDocumentService
{
    CertificateDocument Render(Certificate certificate, CertificateDocumentFormat format);
}
