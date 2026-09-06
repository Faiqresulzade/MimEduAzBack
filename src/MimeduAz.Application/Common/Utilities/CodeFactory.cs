using System.Security.Cryptography;

namespace MimeduAz.Application.Common.Utilities;

/// <summary>
/// Sifariş və sertifikat kodlarını yaradır.
/// Format: sifariş "MIM-7113", sertifikat "MIM-2026-4417".
/// Unikallıq DB yoxlanışı ilə çağıran tərəfdə təmin olunur.
/// </summary>
public static class CodeFactory
{
    private const int MaxAttempts = 25;

    public static string NewOrderCode() => $"MIM-{RandomDigits(4)}";

    public static string NewCertificateCode(DateTime issuedAt) =>
        $"MIM-{issuedAt.Year}-{RandomDigits(4)}";

    /// <summary>
    /// Unikal kod tapana qədər cəhd edir. <paramref name="existsAsync"/> true qaytardıqca yeni kod sınayır.
    /// Cəhdlər tükənərsə daha uzun (6 rəqəmli) suffiks ilə kod qaytarır.
    /// </summary>
    public static async Task<string> UniqueAsync(
        Func<string> generator,
        Func<string, CancellationToken, Task<bool>> existsAsync,
        Func<string> fallbackGenerator,
        CancellationToken ct)
    {
        for (var i = 0; i < MaxAttempts; i++)
        {
            var candidate = generator();
            if (!await existsAsync(candidate, ct))
            {
                return candidate;
            }
        }

        return fallbackGenerator();
    }

    public static string NewOrderCodeLong() => $"MIM-{RandomDigits(6)}";

    public static string NewCertificateCodeLong(DateTime issuedAt) =>
        $"MIM-{issuedAt.Year}-{RandomDigits(6)}";

    private static string RandomDigits(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(new string('0', length));
    }
}
