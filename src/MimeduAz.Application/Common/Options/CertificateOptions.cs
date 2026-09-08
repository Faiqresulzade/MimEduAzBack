namespace MimeduAz.Application.Common.Options;

/// <summary>Sertifikat sənədinin tənzimləmələri. appsettings.json -> "Certificate".</summary>
public sealed class CertificateOptions
{
    public const string SectionName = "Certificate";

    /// <summary>
    /// QR kodun apardığı doğrulama ünvanı. <c>{code}</c> sertifikat kodu ilə əvəz olunur.
    /// </summary>
    public string VerificationUrlTemplate { get; set; } = "https://mimedu.az/sertifikat-yoxla/{code}";

    /// <summary>İmza xəttinin altında yazılan ad/qurum.</summary>
    public string SignatureName { get; set; } = "MIMEDU.AZ";

    /// <summary>İmza xəttinin altında yazılan vəzifə.</summary>
    public string SignatureTitle { get; set; } = "Platforma rəhbərliyi";

    /// <summary>PNG üçün render sıxlığı. 150 ekran+çap üçün balanslı ölçü verir.</summary>
    public int PngDpi { get; set; } = 150;

    public string BuildVerificationUrl(string code) =>
        VerificationUrlTemplate.Replace("{code}", Uri.EscapeDataString(code), StringComparison.OrdinalIgnoreCase);
}
