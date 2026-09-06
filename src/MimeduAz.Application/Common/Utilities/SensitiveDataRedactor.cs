using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MimeduAz.Application.Common.Utilities;

/// <summary>
/// Audit log-a düşən JSON gövdələrindən həssas sahələri təmizləyir.
/// Şifrələr və token-lər heç bir halda verilənlər bazasına yazılmamalıdır.
/// </summary>
public static class SensitiveDataRedactor
{
    public const string Mask = "***";

    /// <summary>
    /// Azərbaycan hərfləri log-da ə kimi yox, oxunaqlı saxlanılsın deyə
    /// relaxed encoder istifadə olunur. Nəticə yalnız DB-yə yazılır və API cavabında
    /// yenidən düzgün serializasiya olunur - HTML kontekstinə birbaşa düşmür.
    /// </summary>
    private static readonly JsonSerializerOptions LogSerializerOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Adı bu siyahıdakı sözlərdən birini ehtiva edən hər JSON sahəsi maskalanır
    /// (böyük/kiçik hərfə həssas deyil, qismən uyğunluq).
    /// </summary>
    private static readonly string[] SensitiveKeyFragments =
    {
        "password",
        "token",
        "secret",
        "authorization",
        "apikey",
        "cardnumber",
        "cardholder",
        "cvc",
        "cvv"
    };

    /// <summary>
    /// JSON gövdəni maskalayıb qaytarır. JSON deyilsə məzmun tamamilə buraxılır -
    /// tanınmayan formatda şifrənin gizli qalacağına zəmanət yoxdur.
    /// </summary>
    public static string? Redact(string? body, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return Truncate("[JSON olmayan gövdə loglanmadı]", maxLength);
        }

        if (node is null)
        {
            return null;
        }

        RedactNode(node);

        var redacted = node.ToJsonString(LogSerializerOptions);
        return Truncate(redacted, maxLength);
    }

    private static void RedactNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                // Açar siyahısını əvvəlcədən götürürük - iterasiya zamanı dəyişiklik edirik.
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSensitive(key))
                    {
                        obj[key] = Mask;
                        continue;
                    }

                    if (obj[key] is { } child)
                    {
                        RedactNode(child);
                    }
                }
                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        RedactNode(item);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Yuxarıdakı fraqmentləri ehtiva etsə də həssas olmayan sahələr.
    /// Məs. "accessTokenExpiresAt" sadəcə vaxt damğasıdır - maskalamağa dəyməz.
    /// </summary>
    private static readonly string[] SafeKeySuffixes =
    {
        "expiresat",
        "expiryminutes",
        "expirydays"
    };

    private static bool IsSensitive(string key)
    {
        if (SafeKeySuffixes.Any(suffix => key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return SensitiveKeyFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static string? Truncate(string? value, int maxLength)
    {
        if (value is null || maxLength <= 0 || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + $"... [{value.Length - maxLength} simvol kəsildi]";
    }
}
