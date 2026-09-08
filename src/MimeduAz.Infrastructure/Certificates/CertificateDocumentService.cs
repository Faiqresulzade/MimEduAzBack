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

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly object FontLock = new();
    private static bool _fontsRegistered;

    private readonly CertificateOptions _options;

    public CertificateDocumentService(IOptions<CertificateOptions> options)
    {
        _options = options.Value;

        // QuestPDF Community lisenziyası: açıq mənbə və kiçik təşkilatlar üçün pulsuzdur.
        QuestPDF.Settings.License = LicenseType.Community;

        EnsureFontsRegistered();
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
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontColor("#1F2937"));

                // Haşiyə fon qatındadır: məzmun axınına təsir etmir, ona görə uzun
                // mətn onu sıxışdıra bilmir.
                page.Background().Element(DrawFrame);

                // Footer ayrı slotdadır - məzmun nə qədər uzun olsa da QR və imza
                // həmişə birinci səhifənin dibində qalır.
                page.Content().PaddingHorizontal(52).PaddingTop(44)
                    .Element(content => ComposeBody(content, certificate));

                page.Footer().PaddingHorizontal(52).PaddingBottom(40)
                    .Element(footer => ComposeFooter(footer, certificate, qr, verificationUrl));
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

    /// <summary>Qızılı ikiqat haşiyə. Fon qatındadır - məzmun axınına təsir etmir.</summary>
    private static void DrawFrame(IContainer container) =>
        container
            .Background("#FFFFFF")
            .Padding(18)
            .Border(3)
            .BorderColor("#C9A227")
            .Padding(6)
            .Border(1)
            .BorderColor("#C9A227");

    private void ComposeBody(IContainer container, Certificate certificate)
    {
        var holderName = string.IsNullOrWhiteSpace(certificate.User?.FullName)
            ? "—"
            : Shorten(certificate.User!.FullName, 60);

        var trainingName = certificate.Training?.Name;
        var issuedAt = certificate.IssuedAt.ToString("dd.MM.yyyy", Culture);

        container.Column(column =>
        {
            column.Spacing(0);

            // ---------- Başlıq ----------
            column.Item().AlignCenter().Text(text =>
            {
                text.Span("MIMEDU").FontSize(20).Bold().FontColor("#0F172A").LetterSpacing(0.12f);
                text.Span(".AZ").FontSize(20).Bold().FontColor("#2563EB").LetterSpacing(0.12f);
            });

            column.Item().PaddingTop(2).AlignCenter()
                .Text("Müəllimlər üçün rəqəmsal təhsil platforması")
                .FontSize(8).FontColor("#6B7280").LetterSpacing(0.08f);

            column.Item().PaddingTop(18).AlignCenter()
                .Text("SERTİFİKAT")
                .FontSize(38).Bold().FontColor("#0F172A").LetterSpacing(0.22f);

            column.Item().PaddingTop(6).AlignCenter()
                .Width(120).LineHorizontal(2).LineColor("#C9A227");

            // ---------- Əsas mətn ----------
            column.Item().PaddingTop(20).AlignCenter()
                .Text("Bu sertifikat təsdiq edir ki,")
                .FontSize(11).FontColor("#4B5563");

            column.Item().PaddingTop(8).AlignCenter().MaxWidth(620)
                .Text(holderName)
                .FontSize(28).Bold().FontColor("#1D4ED8").AlignCenter();

            column.Item().PaddingTop(4).AlignCenter()
                .Width(320).LineHorizontal(1).LineColor("#E5E7EB");

            if (!string.IsNullOrWhiteSpace(trainingName))
            {
                column.Item().PaddingTop(14).AlignCenter()
                    .Text("aşağıdakı təlimi uğurla tamamlamışdır:")
                    .FontSize(11).FontColor("#4B5563");

                column.Item().PaddingTop(6).AlignCenter().MaxWidth(560)
                    .Text($"«{Shorten(trainingName, 110)}»")
                    .FontSize(16).Bold().FontColor("#0F172A").AlignCenter();

                if (certificate.Training?.DurationHours is > 0)
                {
                    column.Item().PaddingTop(6).AlignCenter()
                        .Text($"{certificate.Training.DurationHours} saat")
                        .FontSize(10).FontColor("#6B7280");
                }
            }
            else
            {
                // Resurs imtahanı sertifikatı - təsvir mətni özü hər şeyi izah edir.
                column.Item().PaddingTop(14).AlignCenter().MaxWidth(600)
                    .Text(Shorten(certificate.Description, 200))
                    .FontSize(12).FontColor("#374151").AlignCenter();
            }

            column.Item().PaddingTop(10).AlignCenter()
                .Text($"Verilmə tarixi: {issuedAt}")
                .FontSize(10).FontColor("#6B7280");
        });
    }

    private void ComposeFooter(
        IContainer container, Certificate certificate, byte[] qr, string verificationUrl)
    {
        container.Row(row =>
        {
            row.ConstantItem(150).Column(qrColumn =>
            {
                qrColumn.Item().Width(62).Height(62).Image(qr).FitArea();

                qrColumn.Item().PaddingTop(4)
                    .Text(certificate.Code)
                    .FontSize(9).Bold().FontColor("#111827");

                qrColumn.Item()
                    .Text("Kodu skan edib yoxlayın")
                    .FontSize(6.5f).FontColor("#9CA3AF");
            });

            row.RelativeItem().AlignBottom().AlignCenter()
                .Text(verificationUrl)
                .FontSize(6.5f).FontColor("#C7CBD1");

            row.ConstantItem(210).Column(signature =>
            {
                // Stilizasiya olunmuş "imza" - əl yazısı təqlidi kursiv mətn.
                signature.Item().AlignCenter()
                    .Text("MIMEDU.AZ")
                    .FontSize(22).Italic().FontColor("#1D4ED8").LetterSpacing(0.05f);

                // Descender-lərə toxunmasın deyə xətt bir qədər aşağı salınır.
                signature.Item().PaddingTop(7).AlignCenter()
                    .Width(180).LineHorizontal(1).LineColor("#9CA3AF");

                signature.Item().PaddingTop(4).AlignCenter()
                    .Text(_options.SignatureName)
                    .FontSize(9.5f).Bold().FontColor("#111827");

                signature.Item().AlignCenter()
                    .Text(_options.SignatureTitle)
                    .FontSize(8).FontColor("#6B7280");
            });
        });
    }

    /// <summary>Çox uzun mətnin sertifikatı ikinci səhifəyə daşımasının qarşısını alır.</summary>
    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)].TrimEnd() + "…";

    private static byte[] GenerateQrPng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);

        // PngByteQRCode System.Drawing tələb etmir - Linux konteynerində də işləyir.
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(12);
    }

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
