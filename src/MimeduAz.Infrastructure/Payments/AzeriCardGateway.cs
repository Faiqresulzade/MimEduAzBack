using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Infrastructure.Payments;

/// <summary>
/// AzeriCard e-commerce gateway (hosted redirect). Kart məlumatı bankın səhifəsində daxil edilir;
/// biz yalnız imzalanmış sifariş sahələrini göndəririk və imzalı cavabı yoxlayırıq.
/// </summary>
public sealed partial class AzeriCardGateway : IPaymentGateway, IDisposable
{
    private readonly HttpClient _http;
    private readonly AzeriCardOptions _options;
    private readonly ILogger<AzeriCardGateway> _logger;
    private readonly Lazy<RSA> _privateKey;
    private readonly Lazy<RSA> _gatewayKey;

    public AzeriCardGateway(HttpClient http, IOptions<AzeriCardOptions> options, ILogger<AzeriCardGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _privateKey = new Lazy<RSA>(() => AzeriCardSigner.LoadKey(_options.PrivateKeyPem, _options.PrivateKeyPath));
        _gatewayKey = new Lazy<RSA>(() => AzeriCardSigner.LoadKey(_options.GatewayPublicKeyPem, _options.GatewayPublicKeyPath));
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Terminal)
        && AzeriCardSigner.HasKey(_options.PrivateKeyPem, _options.PrivateKeyPath)
        && AzeriCardSigner.HasKey(_options.GatewayPublicKeyPem, _options.GatewayPublicKeyPath);

    public PaymentRedirect CreateSale(Order order, string description, string? email, string? clientInfo)
    {
        var fields = new Dictionary<string, string>
        {
            ["AMOUNT"] = FormatAmount(order.TotalAmount),
            ["CURRENCY"] = _options.Currency,
            ["ORDER"] = order.PaymentNumber!,
            ["DESC"] = Truncate(description, 50),
            ["MERCH_NAME"] = Truncate(_options.MerchantName, 50),
            ["MERCH_URL"] = _options.MerchantUrl,
            ["TERMINAL"] = _options.Terminal!,
            ["TRTYPE"] = "1",
            ["COUNTRY"] = _options.Country,
            ["MERCH_GMT"] = _options.MerchantGmt,
            ["TIMESTAMP"] = Timestamp(),
            ["NONCE"] = Nonce(),
            ["BACKREF"] = _options.BackrefUrl,
            ["LANG"] = _options.Lang
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            fields["EMAIL"] = Truncate(email, 80);
        }

        // 3-D Secure 2 üçün brauzer məlumatı (ekran ölçüsü, saat qurşağı) - Base64 JSON.
        if (!string.IsNullOrWhiteSpace(clientInfo))
        {
            fields["M_INFO"] = clientInfo;
        }

        fields["P_SIGN"] = AzeriCardSigner.Sign(
            AzeriCardSigner.BuildSource(fields, AzeriCardSigner.SaleFields), _privateKey.Value);

        return new PaymentRedirect(_options.GatewayUrl, fields);
    }

    public PaymentResult ParseCallback(IReadOnlyDictionary<string, string> fields) => ToResult(fields);

    public Task<PaymentResult> CompleteAsync(Order order, CancellationToken ct) =>
        SendReferencedAsync(order, "21", ct);

    public Task<PaymentResult> RefundAsync(Order order, CancellationToken ct) =>
        SendReferencedAsync(order, "22", ct);

    public async Task<PaymentResult> QueryStatusAsync(Order order, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["TRAN_TRTYPE"] = "1",
            ["ORDER"] = order.PaymentNumber!,
            ["TERMINAL"] = _options.Terminal!,
            ["TRTYPE"] = "90",
            ["TIMESTAMP"] = Timestamp(),
            ["NONCE"] = Nonce()
        };
        fields["P_SIGN"] = AzeriCardSigner.Sign(
            AzeriCardSigner.BuildSource(fields, AzeriCardSigner.StatusFields), _privateKey.Value);

        return await PostAsync(fields, ct);
    }

    /// <summary>Əvvəlki əməliyyata (RRN + INT_REF) istinad edən sorğular: tamamlama və qaytarma.</summary>
    private async Task<PaymentResult> SendReferencedAsync(Order order, string trType, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["AMOUNT"] = FormatAmount(order.TotalAmount),
            ["CURRENCY"] = _options.Currency,
            ["ORDER"] = order.PaymentNumber!,
            ["RRN"] = order.Rrn ?? string.Empty,
            ["INT_REF"] = order.IntRef ?? string.Empty,
            ["TERMINAL"] = _options.Terminal!,
            ["TRTYPE"] = trType,
            ["TIMESTAMP"] = Timestamp(),
            ["NONCE"] = Nonce(),
            ["LANG"] = _options.Lang
        };
        fields["P_SIGN"] = AzeriCardSigner.Sign(
            AzeriCardSigner.BuildSource(fields, AzeriCardSigner.CompletionFields), _privateKey.Value);

        return await PostAsync(fields, ct);
    }

    private async Task<PaymentResult> PostAsync(Dictionary<string, string> fields, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        using var response = await _http.PostAsync(_options.GatewayUrl, new FormUrlEncodedContent(fields), timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AzeriCard {TrType} sorğusu HTTP {Status} qaytardı.", fields["TRTYPE"], (int)response.StatusCode);
        }

        return ToResult(ParseResponse(body));
    }

    private PaymentResult ToResult(IReadOnlyDictionary<string, string> raw)
    {
        // Açarlar həmişə böyük hərflə müqayisə olunur.
        var fields = raw.ToDictionary(p => p.Key.ToUpperInvariant(), p => p.Value.Trim());

        // Açar qurulmayıbsa heç bir cavaba güvənmirik - imza etibarsız sayılır (500 yox).
        var valid = false;
        if (AzeriCardSigner.HasKey(_options.GatewayPublicKeyPem, _options.GatewayPublicKeyPath))
        {
            try
            {
                valid = AzeriCardSigner.Verify(
                    AzeriCardSigner.BuildSource(fields, AzeriCardSigner.ResponseFields),
                    fields.GetValueOrDefault("P_SIGN"),
                    _gatewayKey.Value);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning("AzeriCard imzası yoxlanmadı: {Error}", ex.Message);
            }
        }

        return new PaymentResult(
            valid,
            fields.GetValueOrDefault("ORDER"),
            fields.GetValueOrDefault("AMOUNT"),
            fields.GetValueOrDefault("CURRENCY"),
            fields.GetValueOrDefault("TERMINAL"),
            fields.GetValueOrDefault("ACTION"),
            fields.GetValueOrDefault("RC"),
            NullIfEmpty(fields.GetValueOrDefault("RRN")),
            NullIfEmpty(fields.GetValueOrDefault("INT_REF")),
            NullIfEmpty(fields.GetValueOrDefault("APPROVAL")),
            NullIfEmpty(fields.GetValueOrDefault("CARD")),
            fields);
    }

    /// <summary>
    /// Server-server cavabı HTML, XML və ya JSON ola bilər (sənəd, bölmə 8.2).
    /// Hər üçü və sadə "KEY=VALUE" sətirləri dəstəklənir.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseResponse(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var text = body.Trim();
        if (text.Length == 0)
        {
            return result;
        }

        if (text.StartsWith('{'))
        {
            try
            {
                using var json = JsonDocument.Parse(text);
                Flatten(json.RootElement, result);
                return result;
            }
            catch (JsonException)
            {
                // aşağıdakı formatlara keçirik
            }
        }

        if (text.StartsWith('<') && !text.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                foreach (var element in XDocument.Parse(text).Descendants().Where(e => !e.HasElements))
                {
                    result[element.Name.LocalName] = element.Value;
                }
                return result;
            }
            catch (System.Xml.XmlException)
            {
                // aşağıdakı formatlara keçirik
            }
        }

        // HTML formu: <input name="ACTION" value="0">
        foreach (Match match in HtmlInput().Matches(text))
        {
            result[match.Groups["name"].Value] = System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value);
        }

        if (result.Count == 0)
        {
            foreach (var line in text.Split('\n', '&'))
            {
                var index = line.IndexOf('=');
                if (index > 0)
                {
                    result[line[..index].Trim()] = Uri.UnescapeDataString(line[(index + 1)..].Trim());
                }
            }
        }

        return result;
    }

    private static void Flatten(JsonElement element, Dictionary<string, string> result)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(property.Value, result);
            }
            else if (property.Value.ValueKind != JsonValueKind.Array)
            {
                result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText();
            }
        }
    }

    public static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Timestamp() => DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string Nonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [GeneratedRegex("<input[^>]*name=[\"'](?<name>[^\"']+)[\"'][^>]*value=[\"'](?<value>[^\"']*)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlInput();

    public void Dispose()
    {
        if (_privateKey.IsValueCreated) _privateKey.Value.Dispose();
        if (_gatewayKey.IsValueCreated) _gatewayKey.Value.Dispose();
    }
}
