using System.Security.Cryptography;
using System.Text;

namespace MimeduAz.Infrastructure.Payments;

/// <summary>
/// AzeriCard P_SIGN (developer.azericard.com, bölmə 2.2 və 8.3): hər sahənin dəyərinin
/// əvvəlinə onun uzunluğu yazılır, dəyərlər ayırıcısız birləşdirilir, boş dəyər əvəzinə "-".
/// İmza RSA + SHA-256 (PKCS#1 v1.5), nəticə hex.
/// </summary>
public static class AzeriCardSigner
{
    /// <summary>Satış/ön-avtorizasiya (TRTYPE=0/1) imza sahələri.</summary>
    public static readonly string[] SaleFields =
        { "AMOUNT", "CURRENCY", "TERMINAL", "TRTYPE", "TIMESTAMP", "NONCE", "MERCH_URL" };

    /// <summary>Tamamlama və qaytarma (TRTYPE=21/22/24) imza sahələri.</summary>
    public static readonly string[] CompletionFields =
        { "AMOUNT", "CURRENCY", "TERMINAL", "TRTYPE", "ORDER", "RRN", "INT_REF" };

    /// <summary>Status sorğusu (TRTYPE=90) imza sahələri.</summary>
    public static readonly string[] StatusFields =
        { "ORDER", "TERMINAL", "TRTYPE", "TIMESTAMP", "NONCE" };

    /// <summary>Bankın cavabının (callback) yoxlanma sahələri.</summary>
    public static readonly string[] ResponseFields =
        { "AMOUNT", "TERMINAL", "APPROVAL", "RRN", "INT_REF" };

    /// <summary>
    /// "5" + "11.48" + "3" + "USD" ... Boş dəyər uzunluqsuz "-" kimi yazılır (sənəddə callback üçün
    /// belə göstərilib; sorğularda boş sahə olmur — bütün imza sahələri doludur).
    /// </summary>
    public static string BuildSource(IReadOnlyDictionary<string, string> fields, IEnumerable<string> order)
    {
        var builder = new StringBuilder();
        foreach (var name in order)
        {
            if (fields.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
            {
                builder.Append(Encoding.UTF8.GetByteCount(value)).Append(value);
            }
            else
            {
                builder.Append('-');
            }
        }

        return builder.ToString();
    }

    public static string Sign(string source, RSA privateKey)
    {
        var signature = privateKey.SignData(
            Encoding.UTF8.GetBytes(source), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    public static bool Verify(string source, string? hexSignature, RSA publicKey)
    {
        if (string.IsNullOrWhiteSpace(hexSignature))
        {
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromHexString(hexSignature.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        return publicKey.VerifyData(
            Encoding.UTF8.GetBytes(source), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>Env dəyişənində PEM çox vaxt "\n" ilə tək sətirdə saxlanılır.</summary>
    public static RSA LoadKey(string? pem, string? path)
    {
        var text = !string.IsNullOrWhiteSpace(pem)
            ? pem.Replace("\\n", "\n")
            : !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? File.ReadAllText(path)
                : throw new InvalidOperationException("AzeriCard açarı tapılmadı.");

        var rsa = RSA.Create();
        rsa.ImportFromPem(text);
        return rsa;
    }

    public static bool HasKey(string? pem, string? path) =>
        !string.IsNullOrWhiteSpace(pem) || (!string.IsNullOrWhiteSpace(path) && File.Exists(path));
}
