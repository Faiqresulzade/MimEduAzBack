using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Entities;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MimeduAz.Infrastructure.Certificates;

/// <summary>
/// Sertifikatı A4 (landşaft) ölçüsündə PNG və ya PDF kimi qurur.
/// Şrift layihəyə əlavə edilib: sistem şriftlərində Azərbaycan «ə» hərfi
/// (U+0259) çox vaxt olmur, konteynerdə isə ümumiyyətlə şrift yoxdur.
/// </summary>
public sealed class CertificateDocumentService : ICertificateDocumentService
{
    private const string FontFamily = "Noto Sans";

    // Dizaynın rəngləri
    private const string Navy = "#1B2A5B";
    private const string NavySoft = "#3A4A7B";
    private const string Gold = "#C9A227";
    private const string Muted = "#6B7280";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly object FontLock = new();
    private static bool _fontsRegistered;

    private readonly CertificateOptions _options;
    private readonly byte[]? _logo;
    private readonly byte[]? _stamp;
    private readonly byte[]? _signature;

    public CertificateDocumentService(IOptions<CertificateOptions> options)
    {
        _options = options.Value;

        // QuestPDF Community lisenziyası: açıq mənbə və kiçik təşkilatlar üçün pulsuzdur.
        QuestPDF.Settings.License = LicenseType.Community;

        EnsureFontsRegistered();

        // Şəkillər opsionaldır - yoxdursa sertifikat onlarsız da düzgün qurulur.
        _logo = TryReadImage(_options.LogoPath);
        _stamp = TryReadImage(_options.StampPath);
        _signature = TryReadImage(_options.SignatureImagePath);
    }

    public CertificateDocument Render(Certificate certificate, CertificateDocumentFormat format)
    {
        var verificationUrl = _options.BuildVerificationUrl(certificate.Code);
        var qr = GenerateQrPng(verificationUrl);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontColor(Navy));

                // Haşiyə fon qatındadır: məzmun axınına təsir etmir, ona görə uzun
                // mətn onu sıxışdıra bilmir.
                page.Background().Element(DrawFrame);

                // Footer ayrı slotdadır - məzmun nə qədər uzun olsa da imza və QR
                // həmişə birinci səhifənin dibində qalır.
                page.Content().PaddingHorizontal(58).PaddingTop(30).PaddingBottom(10)
                    .AlignMiddle()
                    .Element(content => ComposeBody(content, certificate));

                page.Footer().PaddingHorizontal(58).PaddingBottom(32)
                    .Element(footer => ComposeFooter(footer, certificate, qr));
            });
        });

        var safeCode = certificate.Code.Replace('/', '-');

        return format switch
        {
            CertificateDocumentFormat.Pdf => new CertificateDocument(
                document.GeneratePdf(),
                "application/pdf",
                $"mimedu-sertifikat-{safeCode}.pdf"),

            _ => new CertificateDocument(
                document.GenerateImages(new ImageGenerationSettings
                {
                    ImageFormat = ImageFormat.Png,
                    RasterDpi = _options.PngDpi
                }).First(),
                "image/png",
                $"mimedu-sertifikat-{safeCode}.png")
        };
    }

    /// <summary>
    /// Tünd göy ikiqat haşiyə və künc aksentləri. Fon qatındadır - məzmun axınına
    /// təsir etmir.
    /// </summary>
    private static void DrawFrame(IContainer container) =>
        container
            .Background("#FFFFFF")
            .Padding(14)
            .Background(Navy)
            .Padding(9)
            .Background("#FFFFFF")
            .Padding(5)
            .Border(1)
            .BorderColor(Gold)
            .Padding(3)
            .Border(1)
            .BorderColor("#D8DEEB");

    private void ComposeBody(IContainer container, Certificate certificate)
    {
        var holderName = string.IsNullOrWhiteSpace(certificate.User?.FullName)
            ? "—"
            : Shorten(certificate.User!.FullName, 55);

        var trainingName = certificate.Training?.Name;
        var hours = certificate.Training?.DurationHours ?? 0;

        container.Column(column =>
        {
            column.Spacing(0);

            // ---------- Emblem və təşkilat adı ----------
            column.Item().AlignCenter().Element(DrawEmblem);

            column.Item().PaddingTop(6).AlignCenter()
                .Text(_options.OrganizationName)
                .FontSize(9).Bold().FontColor(NavySoft).LetterSpacing(0.14f);

            // ---------- Başlıq ----------
            column.Item().PaddingTop(14).AlignCenter()
                .Text("SERTİFİKAT")
                .FontSize(42).Bold().FontColor(Navy).LetterSpacing(0.2f);

            column.Item().PaddingTop(4).AlignCenter()
                .Width(150).LineHorizontal(1.5f).LineColor(Gold);

            // ---------- Sahibin adı ----------
            column.Item().PaddingTop(22).AlignCenter().MaxWidth(600)
                .Text(holderName)
                .FontSize(27).Bold().FontColor(Navy).AlignCenter();

            column.Item().PaddingTop(3).AlignCenter()
                .Width(300).LineHorizontal(0.8f).LineColor("#C7CEDF");

            // ---------- Nəyə görə ----------
            if (!string.IsNullOrWhiteSpace(trainingName))
            {
                column.Item().PaddingTop(14).AlignCenter().MaxWidth(620)
                    .Text($"«{Shorten(trainingName, 100)}»")
                    .FontSize(14).Italic().FontColor(NavySoft).AlignCenter();

                column.Item().PaddingTop(5).AlignCenter().MaxWidth(600).Text(text =>
                {
                    text.AlignCenter();
                    text.DefaultTextStyle(x => x.FontSize(11).FontColor(Muted));

                    if (hours > 0)
                    {
                        text.Span($"təlimini ({hours} saat) uğurla tamamladığına görə");
                    }
                    else
                    {
                        text.Span("təlimini uğurla tamamladığına görə");
                    }
                });
            }
            else
            {
                // Resurs imtahanı sertifikatı: mümkünsə struktur məlumatdan cümlə qururuq,
                // əks halda hazır təsvir mətninə keçirik.
                var attempt = certificate.ResourceQuizAttempt;
                var resourceName = attempt?.Quiz?.Resource?.Name;

                if (!string.IsNullOrWhiteSpace(resourceName))
                {
                    column.Item().PaddingTop(14).AlignCenter().MaxWidth(620)
                        .Text($"«{Shorten(resourceName, 100)}»")
                        .FontSize(14).Italic().FontColor(NavySoft).AlignCenter();

                    column.Item().PaddingTop(5).AlignCenter().MaxWidth(600)
                        .Text($"materialı üzrə imtahandan {attempt!.ScorePercent}% nəticə göstərdiyinə görə")
                        .FontSize(11).FontColor(Muted).AlignCenter();
                }
                else
                {
                    column.Item().PaddingTop(14).AlignCenter().MaxWidth(620)
                        .Text(Shorten(certificate.Description, 190))
                        .FontSize(12).Italic().FontColor(NavySoft).AlignCenter();
                }
            }

            column.Item().PaddingTop(8).AlignCenter()
                .Text("təltif edilir")
                .FontSize(15).Bold().FontColor(Navy).LetterSpacing(0.05f);
        });
    }

    private void ComposeFooter(IContainer container, Certificate certificate, byte[] qr)
    {
        var issuedAt = certificate.IssuedAt.ToString("dd.MM.yyyy", Culture);

        container.Row(row =>
        {
            // ---------- Sol: QR + kod + tarix ----------
            row.ConstantItem(150).Column(left =>
            {
                left.Item().Width(58).Height(58).Image(qr).FitArea();

                left.Item().PaddingTop(4)
                    .Text(certificate.Code)
                    .FontSize(9).Bold().FontColor(Navy);

                left.Item()
                    .Text($"Verilmə tarixi: {issuedAt}")
                    .FontSize(7).FontColor(Muted);
            });

            // ---------- Orta: möhür (varsa) ----------
            row.RelativeItem().AlignBottom().AlignCenter().Element(middle =>
            {
                if (_stamp is not null)
                {
                    middle.Width(88).Height(88).Image(_stamp).FitArea();
                }
                else
                {
                    middle.Height(1);
                }
            });

            // ---------- Sağ: imza bloku ----------
            row.ConstantItem(250).Column(signature =>
            {
                if (_signature is not null)
                {
                    signature.Item().AlignCenter().Height(34).Image(_signature).FitHeight();
                }
                else
                {
                    // Şəkil verilməyibsə stilizasiya olunmuş mətn imzası.
                    signature.Item().AlignCenter()
                        .Text(_options.SignatureName)
                        .FontSize(19).Italic().FontColor("#2743A0");
                }

                signature.Item().PaddingTop(6).AlignCenter()
                    .Width(215).LineHorizontal(0.8f).LineColor("#9CA3AF");

                signature.Item().PaddingTop(4).AlignCenter()
                    .Text(_options.SignatureTitle)
                    .FontSize(8).FontColor(Muted).AlignCenter();

                // Ad yalnız real imza şəkli varsa xəttin altında təkrarlanır -
                // mətn imzasında onsuz da ad yuxarıda yazılıb.
                if (_signature is not null)
                {
                    signature.Item().AlignCenter()
                        .Text(_options.SignatureName)
                        .FontSize(10).Bold().FontColor(Navy);
                }
            });
        });
    }

    /// <summary>Loqo şəkli varsa onu, yoxsa qısa adı olan dairəvi emblem çəkir.</summary>
    private void DrawEmblem(IContainer container)
    {
        if (_logo is not null)
        {
            container.Height(56).Image(_logo).FitHeight();
            return;
        }

        container
            .Width(52).Height(52)
            .Background(Navy)
            .AlignMiddle()
            .AlignCenter()
            .Text(_options.OrganizationShortName)
            .FontSize(15).Bold().FontColor("#FFFFFF").LetterSpacing(0.06f);
    }

    private static byte[]? TryReadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path);

            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }
        catch (Exception)
        {
            // Şəkil oxuna bilmirsə sertifikat onsuz qurulur - render heç vaxt sınmamalıdır.
            return null;
        }
    }

    private static byte[] GenerateQrPng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);

        // PngByteQRCode System.Drawing tələb etmir - Linux konteynerində də işləyir.
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(12);
    }

    /// <summary>Çox uzun mətnin sertifikatı ikinci səhifəyə daşımasının qarşısını alır.</summary>
    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)].TrimEnd() + "…";

    /// <summary>
    /// Şriftləri bir dəfə qeydiyyatdan keçirir. QuestPDF-in şrift reyestri qlobaldır,
    /// ona görə təkrar qeydiyyat lazım deyil.
    /// </summary>
    private static void EnsureFontsRegistered()
    {
        if (_fontsRegistered)
        {
            return;
        }

        lock (FontLock)
        {
            if (_fontsRegistered)
            {
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();

            foreach (var name in assembly.GetManifestResourceNames()
                         .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is not null)
                {
                    FontManager.RegisterFont(stream);
                }
            }

            _fontsRegistered = true;
        }
    }
}
