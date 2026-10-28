namespace MimeduAz.Application.Common.Options;

/// <summary>KSQ cavab kartı yoxlaması. appsettings.json -> "Ksq".</summary>
public sealed class KsqOptions
{
    public const string SectionName = "Ksq";

    /// <summary>Bir müəllimin dəqiqədə göndərə biləcəyi şəkil sayı.</summary>
    public int ScansPerMinute { get; set; } = 20;

    /// <summary>Bir müəllimin sutkada göndərə biləcəyi şəkil sayı.</summary>
    public int ScansPerDay { get; set; } = 500;

    /// <summary>Qəbul olunan şəklin maksimum ölçüsü. Frontend şəkli onsuz da kiçildir.</summary>
    public long MaxImageBytes { get; set; } = 8 * 1024 * 1024;
}

/// <summary>
/// OpenRouter (vision model) tənzimləmələri. appsettings.json -> "OpenRouter".
/// API açarı yalnız user-secrets və ya env ilə verilir: <c>OpenRouter:ApiKey</c> və ya <c>OPENROUTER_API_KEY</c>.
/// </summary>
public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";

    public string VisionModel { get; set; } = "google/gemini-2.5-flash";

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>OpenRouter-in statistikasında görünən sayt ünvanı və adı.</summary>
    public string SiteUrl { get; set; } = "https://mimedu.az";

    public string AppName { get; set; } = "MIMEDU.AZ";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
