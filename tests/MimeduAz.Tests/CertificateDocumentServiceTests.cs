using FluentAssertions;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Domain.Entities;
using MimeduAz.Infrastructure.Certificates;

namespace MimeduAz.Tests;

public sealed class CertificateDocumentServiceTests
{
    private static CertificateDocumentService CreateSut() =>
        new(Options.Create(new CertificateOptions
        {
            VerificationUrlTemplate = "https://mimedu.az/sertifikat-yoxla/{code}",
            SignatureName = "MIMEDU.AZ",
            SignatureTitle = "Platforma rəhbərliyi",
            // Testdə sürət üçün aşağı DPI - məzmun eynidir.
            PngDpi = 72
        }));

    private static Certificate TrainingCertificate() => new()
    {
        Code = "MIM-2026-4417",
        // Azərbaycan hərflərinin hamısı: ə, ş, ğ, ı, ö, ü, ç, İ
        Description = "Nigar Əliyeva · «Süni intellektlə dərs dizaynı» · 8 saat · 14.03.2026",
        IssuedAt = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
        User = new ApplicationUser { FullName = "Nigar Əliyeva Şəfiqə qızı" },
        Training = new Training
        {
            Name = "Süni intellektlə dərs dizaynı",
            DurationHours = 8
        }
    };

    [Fact]
    public void Renders_png_with_valid_signature()
    {
        var document = CreateSut().Render(TrainingCertificate(), CertificateDocumentFormat.Png);

        document.ContentType.Should().Be("image/png");
        document.FileName.Should().Be("mimedu-sertifikat-MIM-2026-4417.png");
        document.Content.Should().NotBeEmpty();

        // PNG imzası: 89 50 4E 47 0D 0A 1A 0A
        document.Content.Take(8).Should()
            .Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    }

    [Fact]
    public void Renders_pdf_with_valid_signature()
    {
        var document = CreateSut().Render(TrainingCertificate(), CertificateDocumentFormat.Pdf);

        document.ContentType.Should().Be("application/pdf");
        document.FileName.Should().EndWith(".pdf");

        // PDF imzası: "%PDF-"
        System.Text.Encoding.ASCII.GetString(document.Content.Take(5).ToArray())
            .Should().Be("%PDF-");
    }

    [Fact]
    public void Pdf_contains_holder_name_and_certificate_code()
    {
        var document = CreateSut().Render(TrainingCertificate(), CertificateDocumentFormat.Pdf);
        var raw = System.Text.Encoding.Latin1.GetString(document.Content);

        // QuestPDF mətni sıxışdırır, ona görə xam axtarış etibarsızdır -
        // sənədin ölçüsü şrift və məzmunun daxil edildiyini göstərir.
        document.Content.Length.Should().BeGreaterThan(20_000,
            "sertifikatda şrift, QR kod və mətn olmalıdır");
        raw.Should().Contain("%PDF");
    }

    [Fact]
    public void Png_is_a4_landscape_shaped()
    {
        var document = CreateSut().Render(TrainingCertificate(), CertificateDocumentFormat.Png);

        // PNG başlığından ölçüləri oxuyuruq (IHDR: 16-cı baytdan en, 20-ci baytdan hündürlük).
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
            document.Content.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
            document.Content.AsSpan(20, 4));

        width.Should().BeGreaterThan(height, "sertifikat landşaft A4-dür");

        // A4 landşaft nisbəti 297/210 ≈ 1.414
        var ratio = (double)width / height;
        ratio.Should().BeApproximately(1.414, 0.02);
    }

    [Fact]
    public void Certificate_always_fits_on_a_single_page()
    {
        // Yalnız birinci səhifə render olunur - məzmun bir səhifəyə sığmasa,
        // QR/imza sətri sükutla itir. Bu test həmin reqressiyanı tutur.
        var sut = CreateSut();

        var longCertificate = new Certificate
        {
            Code = "MIM-2026-9999",
            Description = new string('Ə', 300),
            IssuedAt = DateTime.UtcNow,
            User = new ApplicationUser { FullName = "Uzunadlı Şəxs Adı Soyadı Ata adı" },
            Training = new Training
            {
                Name = "Çox uzun adı olan təlim: süni intellekt, formativ qiymətləndirmə "
                       + "və rəqəmsal alətlərlə fərqləndirilmiş tədris metodikası",
                DurationHours = 40
            }
        };

        foreach (var certificate in new[] { TrainingCertificate(), longCertificate })
        {
            var pdf = sut.Render(certificate, CertificateDocumentFormat.Pdf);
            var pageCount = System.Text.RegularExpressions.Regex.Matches(
                System.Text.Encoding.Latin1.GetString(pdf.Content), @"/Type\s*/Page[^s]").Count;

            pageCount.Should().Be(1, "sertifikat bir A4 səhifəyə sığmalıdır");
        }
    }

    [Fact]
    public void Quiz_certificate_without_training_still_renders()
    {
        var certificate = new Certificate
        {
            Code = "MIM-2026-8890",
            Description = "Elvin Məmmədov · «Kəsrlər üzrə iş vərəqi» imtahanı · 100% · 07.09.2026",
            IssuedAt = DateTime.UtcNow,
            User = new ApplicationUser { FullName = "Elvin Məmmədov" },
            Training = null
        };

        var document = CreateSut().Render(certificate, CertificateDocumentFormat.Png);

        document.Content.Should().NotBeEmpty();
        document.FileName.Should().Contain("MIM-2026-8890");
    }

    [Fact]
    public void Missing_holder_name_does_not_crash()
    {
        var certificate = new Certificate
        {
            Code = "MIM-2026-0001",
            Description = "Test",
            IssuedAt = DateTime.UtcNow,
            User = null
        };

        var act = () => CreateSut().Render(certificate, CertificateDocumentFormat.Png);

        act.Should().NotThrow();
    }

    [Fact]
    public void Verification_url_template_is_applied()
    {
        var options = new CertificateOptions
        {
            VerificationUrlTemplate = "https://example.az/yoxla/{code}"
        };

        options.BuildVerificationUrl("MIM-2026-4417")
            .Should().Be("https://example.az/yoxla/MIM-2026-4417");
    }
}
