using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;
using MimeduAz.Infrastructure.Persistence;

namespace MimeduAz.Tests.TestSupport;

/// <summary>Testlər üçün InMemory DbContext və sadə saxta (fake) asılılıqlar.</summary>
public static class TestHarness
{
    public static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mimedu-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }

    public static IOptions<CommissionOptions> Commission(decimal percent = 0.20m) =>
        Options.Create(new CommissionOptions { ResourcePercent = percent });

    public static IOptions<FileStorageOptions> FileStorage(long maxBytes = 25 * 1024 * 1024) =>
        Options.Create(new FileStorageOptions
        {
            RootPath = "uploads/resources",
            MaxFileSizeBytes = maxBytes,
            AllowedExtensions = new[] { ".pdf", ".docx", ".pptx" }
        });

    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;
}

public sealed class FakeCurrentUserService : ICurrentUserService
{
    public FakeCurrentUserService(Guid? userId = null, bool isAdmin = false, string? email = null)
    {
        UserId = userId;
        IsAdmin = isAdmin;
        Email = email;
    }

    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public bool IsAuthenticated => UserId is not null;
    public bool IsAdmin { get; set; }

    public Guid RequireUserId() => UserId ?? throw new UnauthorizedException();
}

/// <summary>Faylı diskə yazmadan yadda saxlayan saxta storage.</summary>
public sealed class FakeFileStorageService : IFileStorageService
{
    private readonly Dictionary<string, byte[]> _files = new();

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public async Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory, ct);

        var path = $"uploads/resources/{Guid.NewGuid()}-{Path.GetFileName(fileName)}";
        _files[path] = memory.ToArray();
        return path;
    }

    public Task<Stream> GetAsync(string filePath, CancellationToken ct) =>
        Task.FromResult<Stream>(new MemoryStream(_files[filePath]));

    public Task DeleteAsync(string filePath, CancellationToken ct)
    {
        _files.Remove(filePath);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string filePath) => "/" + filePath;
}

/// <summary>
/// Sertifikat renderini əvəz edən saxta servis — unit testlərdə real PNG/PDF
/// yaradılmasına ehtiyac yoxdur, yalnız çağırıldığını yoxlamaq kifayətdir.
/// </summary>
public sealed class FakeCertificateDocumentService : ICertificateDocumentService
{
    public int RenderCount { get; private set; }
    public CertificateDocumentFormat? LastFormat { get; private set; }

    public CertificateDocument Render(
        MimeduAz.Domain.Entities.Certificate certificate, CertificateDocumentFormat format)
    {
        RenderCount++;
        LastFormat = format;

        var isPdf = format == CertificateDocumentFormat.Pdf;
        return new CertificateDocument(
            new byte[] { 1, 2, 3 },
            isPdf ? "application/pdf" : "image/png",
            $"mimedu-sertifikat-{certificate.Code}.{(isPdf ? "pdf" : "png")}");
    }
}
