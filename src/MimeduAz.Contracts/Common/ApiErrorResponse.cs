namespace MimeduAz.Contracts.Common;

/// <summary>Bütün xətalar üçün standart cavab formatı.</summary>
public sealed class ApiErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>Sahə adı -> xəta mesajları. Yalnız validasiya xətalarında doldurulur.</summary>
    public IDictionary<string, string[]>? Errors { get; set; }

    /// <summary>Log-larla uyğunlaşdırmaq üçün sorğu identifikatoru.</summary>
    public string? TraceId { get; set; }
}
