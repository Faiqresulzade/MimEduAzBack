namespace MimeduAz.Application.Common.Options;

/// <summary>
/// E-poçt göndərmə tənzimləmələri. appsettings.json -> "Email".
/// SMTP doldurulmayıbsa məktub göndərilmir, yalnız loga yazılır —
/// bildiriş sisteminin qurulmamış olması heç bir əməliyyatı pozmamalıdır.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Tamamilə söndürmək üçün. Host boşdursa onsuz da göndərilmir.</summary>
    public bool Enabled { get; set; } = true;

    public string? Host { get; set; }
    public int Port { get; set; } = 587;

    /// <summary>587 üçün STARTTLS, 465 üçün true (implicit SSL).</summary>
    public bool UseSsl { get; set; }

    public string? Username { get; set; }
    public string? Password { get; set; }

    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "MIMEDU.AZ";

    /// <summary>
    /// Moderasiya bildirişinin əlavə olaraq göndəriləcəyi sabit ünvan (opsional).
    /// Boş olsa yalnız bazadakı adminlərə gedir.
    /// </summary>
    public string? ModerationInbox { get; set; }

    /// <summary>Frontend-in ünvanı — məktubdakı linklər üçün.</summary>
    public string SiteUrl { get; set; } = "https://mimedu.az";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>SMTP həqiqətən konfiqurasiya olunubmu.</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromAddress);
}
