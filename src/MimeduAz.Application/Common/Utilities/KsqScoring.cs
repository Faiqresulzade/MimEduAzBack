namespace MimeduAz.Application.Common.Utilities;

/// <summary>
/// KSQ kartının balı. Frontend-dəki <c>computeScore</c> (src/lib/ksq.ts) ilə eyni qaydadır —
/// server client-in balına güvənmir, saxlayanda yenidən hesablayır.
/// </summary>
public static class KsqScoring
{
    public static readonly string[] Letters = { "A", "B", "C", "D", "E" };

    public const int MaxClosed = 60;
    public const int MaxOpen = 10;

    public sealed record Score(decimal Bal, decimal Max, IReadOnlyList<int> Sehv);

    /// <summary>
    /// Qapalı sual: cavab açarla üst-üstə düşürsə sualın balı yazılır, əks halda (boş da daxil)
    /// səhv sayılır. Açıq sual: müəllimin yazdığı bal 0 ilə sualın maksimumu arasında kəsilir.
    /// </summary>
    public static Score Compute(
        IReadOnlyList<int> closedPoints,
        IReadOnlyList<int> openPoints,
        IReadOnlyList<string?> key,
        IReadOnlyList<string?> answers,
        IReadOnlyList<decimal?> openScores)
    {
        decimal bal = 0;
        var sehv = new List<int>();

        for (var i = 0; i < closedPoints.Count; i++)
        {
            var correct = i < key.Count ? key[i] : null;
            var given = i < answers.Count ? answers[i] : null;

            if (correct is not null && given == correct)
            {
                bal += closedPoints[i];
            }
            else
            {
                sehv.Add(i + 1);
            }
        }

        for (var j = 0; j < openPoints.Count; j++)
        {
            var value = j < openScores.Count ? openScores[j] ?? 0 : 0;
            bal += Math.Clamp(value, 0, openPoints[j]);
        }

        var max = closedPoints.Sum() + openPoints.Sum();
        return new Score(bal, max, sehv);
    }

    /// <summary>Variantın açarı yalnız bütün qapalı suallara cavab seçiləndə hazırdır.</summary>
    public static bool KeyReady(IReadOnlyList<string?> key, int closedCount) =>
        key.Count == closedCount && closedCount > 0 && key.All(k => k is not null);

    /// <summary>"a", " B ", "C)" kimi dəyərləri hərfə çevirir; tanınmırsa null.</summary>
    public static string? NormalizeLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().TrimEnd(')', '.', ':').Trim().ToUpperInvariant();
        return trimmed.Length == 1 && Letters.Contains(trimmed) ? trimmed : null;
    }

    /// <summary>Siyahını verilən uzunluğa gətirir: artıq elementlər kəsilir, çatmayanlar <paramref name="fill"/> ilə doldurulur.</summary>
    public static List<T> Normalize<T>(IEnumerable<T>? source, int length, T fill)
    {
        var list = (source ?? Enumerable.Empty<T>()).Take(length).ToList();
        while (list.Count < length)
        {
            list.Add(fill);
        }

        return list;
    }
}
