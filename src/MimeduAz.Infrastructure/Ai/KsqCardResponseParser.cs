using System.Text.Json;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Contracts.Ksq;

namespace MimeduAz.Infrastructure.Ai;

/// <summary>
/// Modelin mətn cavabını <see cref="KsqScanResultDto"/>-ya çevirir. Model bəzən JSON-u
/// ```json blokuna salır və ya əvvəlinə izah yazır — ilk "{" ilə son "}" arası götürülür.
/// Massivlər sual sayına gətirilir, tanınmayan hərflər null olur.
/// </summary>
public static class KsqCardResponseParser
{
    /// <summary>JSON tapılmadı və ya oxunmadı — null.</summary>
    public static KsqScanResultDto? TryParse(string? content, int closedCount, int openCount)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content[start..(end + 1)]);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var cavablar = KsqScoring.Normalize(
                ReadArray(root, "cavablar").Select(KsqScoring.NormalizeLetter), closedCount, null);

            var aciq = KsqScoring.Normalize(
                ReadArray(root, "aciq").Select(v => Clean(v, 500)), openCount, null);

            var unsure = ReadUnsure(root, closedCount + openCount);

            var variant = KsqScoring.NormalizeLetter(ReadString(root, "variant"));
            if (variant is not ("A" or "B"))
            {
                variant = null;
            }

            return new KsqScanResultDto(
                Clean(ReadString(root, "ad"), 200),
                variant,
                Clean(ReadString(root, "kod"), 50),
                cavablar,
                aciq,
                unsure,
                Clean(ReadString(root, "qeyd"), 500));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? AsText(value) : null;

    private static IEnumerable<string?> ReadArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Enumerable.Empty<string?>();
        }

        return value.EnumerateArray().Select(AsText).ToList();
    }

    private static List<int> ReadUnsure(JsonElement root, int maxQuestion)
    {
        if (!root.TryGetProperty("unsure", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return new List<int>();
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind switch
            {
                JsonValueKind.Number when item.TryGetInt32(out var n) => n,
                JsonValueKind.String when int.TryParse(item.GetString(), out var n) => n,
                _ => 0
            })
            .Where(n => n >= 1 && n <= maxQuestion)
            .Distinct()
            .OrderBy(n => n)
            .ToList();
    }

    private static string? AsText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null
    };

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
