namespace MimeduAz.Application.Common.Options;

/// <summary>Sertifikat sənədinin tənzimləmələri. appsettings.json -> "Certificate".</summary>
public sealed class CertificateOptions
{
    public const string SectionName = "Certificate";

    /// <summary>
    /// QR kodun apardığı doğrulama ünvanı. <c>{code}</c> sertifikat kodu ilə əvəz olunur.
    /// </summary>
    public string VerificationUrlTemplate { get; set; } = "https://mimedu.az/sertifikat-yoxla/{code}";

    /// <summary>Sertifikatın başında yazılan təşkilat adı.</summary>
    public string OrganizationName { get; set; } = "MÜƏLLİMLƏRİN İNKİŞAF MƏRKƏZİ";

    /// <summary>Loqo əvəzinə emblemdə göstərilən qısa hərflər (loqo şəkli verilməyibsə).</summary>
    public string OrganizationShortName { get; set; } = "MİM";

    /// <summary>İmza xəttinin üstündə yazılan vəzifə.</summary>
    public string SignatureTitle { get; set; } = "Müəllimlərin İnkişaf Mərkəzinin Direktoru";

    /// <summary>İmza sahibinin adı.</summary>
    public string SignatureName { get; set; } = "VƏFA KƏRİMLİ";

    /// <summary>
    /// Loqo şəklinin yolu (wwwroot-a nisbətən və ya mütləq). Boşdursa mətn emblemi çəkilir.
    /// </summary>
    public string? LogoPath { get; set; }

    /// <summary>
    /// Möhür/ştamp şəklinin yolu. Boşdursa möhür bölməsi ümumiyyətlə çəkilmir —
    /// saxta möhür şəkli çəkməkdənsə boş buraxmaq daha düzgündür.
    /// </summary>
    public string? StampPath { get; set; }

    /// <summary>İmza xəttinin üstündəki əl yazısı görüntüsü. Boşdursa mətn imzası yazılır.</summary>
    public string? SignatureImagePath { get; set; }

    /// <summary>PNG üçün render sıxlığı. 150 ekran+çap üçün balanslı ölçü verir.</summary>
    public int PngDpi { get; set; } = 150;

    public string BuildVerificationUrl(string code) =>
        VerificationUrlTemplate.Replace("{code}", Uri.EscapeDataString(code), StringComparison.OrdinalIgnoreCase);
}
