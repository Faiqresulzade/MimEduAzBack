namespace MimeduAz.Application.Common.Options;

/// <summary>Platforma komissiyası. appsettings.json -> "Commission".</summary>
public sealed class CommissionOptions
{
    public const string SectionName = "Commission";

    /// <summary>Ödənişli resurs satışından platformanın payı (0.20 = 20%). Qalanı müəllifə gedir.</summary>
    public decimal ResourcePercent { get; set; } = 0.20m;
}
