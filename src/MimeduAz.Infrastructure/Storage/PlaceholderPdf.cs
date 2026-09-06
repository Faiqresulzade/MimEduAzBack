using System.Text;

namespace MimeduAz.Infrastructure.Storage;

/// <summary>
/// Seed resursları üçün minimal, amma etibarlı PDF sənədi yaradır.
/// Development mühitində endirmə axınının uçdan-uca yoxlanıla bilməsi üçündür —
/// real materialları əvəz etmir.
/// </summary>
public static class PlaceholderPdf
{
    public static byte[] Create(string title)
    {
        var body = EscapeForPdf(title);

        var objects = new[]
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 4 0 R"
                + "/Resources<</Font<</F1 5 0 R>>>>>>",
            null!, // 4-cü obyekt aşağıda stream kimi qurulur
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>"
        };

        var content = $"BT /F1 16 Tf 60 780 Td ({body}) Tj ET\n"
                    + "BT /F1 11 Tf 60 750 Td (MIMEDU.AZ - numune material \\(seed data\\)) Tj ET";
        var contentBytes = Encoding.ASCII.GetByteCount(content);
        objects[3] = $"<</Length {contentBytes}>>\nstream\n{content}\nendstream";

        using var buffer = new MemoryStream();
        void Write(string s) => buffer.Write(Encoding.ASCII.GetBytes(s));

        Write("%PDF-1.4\n");

        var offsets = new int[objects.Length + 1];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i + 1] = (int)buffer.Length;
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = (int)buffer.Length;
        Write($"xref\n0 {objects.Length + 1}\n");
        Write("0000000000 65535 f \n");
        for (var i = 1; i <= objects.Length; i++)
        {
            Write($"{offsets[i]:D10} 00000 n \n");
        }

        Write($"trailer\n<</Size {objects.Length + 1}/Root 1 0 R>>\nstartxref\n{xrefOffset}\n%%EOF");

        return buffer.ToArray();
    }

    /// <summary>PDF mətn literalında xüsusi simvolları qaçırır və ASCII-yə endirir.</summary>
    private static string EscapeForPdf(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            switch (c)
            {
                case '(':
                case ')':
                case '\\':
                    sb.Append('\\').Append(c);
                    break;
                default:
                    // WinAnsi-dən kənar hərflər (ə, ü, ş, ...) üçün sadə əvəzləmə.
                    sb.Append(c <= 126 ? c : Transliterate(c));
                    break;
            }
        }

        return sb.ToString();
    }

    private static char Transliterate(char c) => char.ToLowerInvariant(c) switch
    {
        'ə' => 'e',
        'ı' => 'i',
        'ö' => 'o',
        'ü' => 'u',
        'ğ' => 'g',
        'ş' => 's',
        'ç' => 'c',
        'i' => 'i',
        _ => '?'
    };
}
