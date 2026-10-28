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

    /// <summary>
    /// Əsas model. 2026-10 benchmark-ı (23 model, 3 kart növü): claude-haiku-5.5 100% dəqiq,
    /// iki işarəli sualı düzgün null edir, 1000 kart ≈ $0.5.
    /// </summary>
    public string VisionModel { get; set; } = "anthropic/claude-haiku-5.5";

    /// <summary>
    /// Əsas model xəta verəndə (limit, kəsilmə) OpenRouter sorğunu sıra ilə bunlara ötürür
    /// (<c>models</c> parametri). Pulsuz nemotron benchmark-da 100% idi, amma yavaş və limitlidir.
    /// </summary>
    public string[] FallbackModels { get; set; } = Array.Empty<string>();

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>OpenRouter-in statistikasında görünən sayt ünvanı və adı.</summary>
    public string SiteUrl { get; set; } = "https://mimedu.az";

    public string AppName { get; set; } = "MIMEDU.AZ";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
