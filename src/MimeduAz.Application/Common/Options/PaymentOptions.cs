namespace MimeduAz.Application.Common.Options;

/// <summary>
/// Kartla ödəniş. appsettings.json -> "Payment".
/// <see cref="Enabled"/> false olduqca ödənişli səbət checkout-u rədd edilir
/// (pulsuz məhsullar işləyir) — AzeriCard terminalı qoşulana qədər belə qalır.
/// </summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    public bool Enabled { get; set; }

    /// <summary>
    /// Hazırkı istifadə şərtlərinin versiyası (dərc tarixi). Qeydiyyatda və ödənişdə
    /// istifadəçinin razılaşdığı versiya kimi saxlanır.
    /// </summary>
    public string TermsVersion { get; set; } = "2026-11-17";

    /// <summary>Bu müddətdə ödənilməyən Pending sifariş Cancelled olur (AzeriCard status sorğusu 24 saatla məhduddur).</summary>
    public int PendingExpiryHours { get; set; } = 24;

    /// <summary>Pending sifarişlərin bankdan statusunun yoxlanma intervalı.</summary>
    public int StatusCheckIntervalMinutes { get; set; } = 10;

    /// <summary>Status yoxlamasına yalnız bu qədər köhnə sifarişlər düşür — istifadəçi hələ bank səhifəsindədir.</summary>
    public int StatusCheckMinAgeMinutes { get; set; } = 15;

    /// <summary>Ödənişdən sonra istifadəçinin qayıtdığı frontend ünvanı.</summary>
    public string SiteUrl { get; set; } = "https://mimedu.az";
}

/// <summary>
/// AzeriCard e-commerce (hosted redirect). appsettings.json -> "AzeriCard".
/// Açarlar yalnız env/secret ilə verilir: <c>AzeriCard__PrivateKeyPem</c>, <c>AzeriCard__GatewayPublicKeyPem</c>
/// (və ya Render Secret File yolu: <c>PrivateKeyPath</c>, <c>GatewayPublicKeyPath</c>).
/// </summary>
public sealed class AzeriCardOptions
{
    public const string SectionName = "AzeriCard";

    /// <summary>Test: https://testmpi.3dsecure.az/cgi-bin/cgi_link. Prod ünvanını bank verir.</summary>
    public string GatewayUrl { get; set; } = "https://testmpi.3dsecure.az/cgi-bin/cgi_link";

    /// <summary>Bankın verdiyi 8 simvollu terminal ID.</summary>
    public string? Terminal { get; set; }

    /// <summary>Kart çıxarışında görünən ad — saytdakı satıcı adı ilə uyğun olmalıdır.</summary>
    public string MerchantName { get; set; } = "MIMEDU.AZ";

    public string MerchantUrl { get; set; } = "https://mimedu.az";

    /// <summary>Bankın nəticəni POST etdiyi ünvan (backend callback).</summary>
    public string BackrefUrl { get; set; } = "https://mimeduazback.onrender.com/api/v1/payments/azericard/callback";

    public string Currency { get; set; } = "AZN";
    public string Country { get; set; } = "AZ";
    public string MerchantGmt { get; set; } = "+4";
    public string Lang { get; set; } = "AZ";

    /// <summary>Merchant RSA private key (PEM). Git-ə qoyulmur.</summary>
    public string? PrivateKeyPem { get; set; }
    public string? PrivateKeyPath { get; set; }

    /// <summary>AzeriCard (MPI) public key (PEM) — cavab imzasını yoxlamaq üçün.</summary>
    public string? GatewayPublicKeyPem { get; set; }
    public string? GatewayPublicKeyPath { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Satıcının hüquqi məlumatları (bank müqaviləsindəki şəxs). appsettings.json -> "Seller".
/// Qəbz məktubunda göstərilir; frontend-dəki src/data/legal.ts ilə eyni olmalıdır.
/// </summary>
public sealed class SellerOptions
{
    public const string SectionName = "Seller";

    public string Name { get; set; } = string.Empty;
    public string Voen { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
