using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Options;

namespace MimeduAz.Infrastructure.Storage;

/// <summary>
/// Faylları local diskdə <c>wwwroot/uploads/resources</c> altında saxlayır.
/// Cloud storage-a keçid üçün yalnız bu sinif <see cref="IFileStorageService"/>-in
/// başqa implementasiyası ilə əvəz olunmalıdır.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly string _webRootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IOptions<FileStorageOptions> options,
        string webRootPath,
        ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _webRootPath = webRootPath;
        _logger = logger;
    }

    public async Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken ct)
    {
        var safeName = SanitizeFileName(fileName);
        var storedName = $"{Guid.NewGuid()}-{safeName}";
        var relativePath = Path.Combine(_options.RootPath, storedName).Replace('\\', '/');

        var absolutePath = ToAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var target = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(target, ct);
        }

        _logger.LogInformation("Fayl saxlanıldı: {RelativePath}", relativePath);
        return relativePath;
    }

    public Task<Stream> GetAsync(string filePath, CancellationToken ct)
    {
        var absolutePath = ToAbsolutePath(filePath);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Fayl tapılmadı.", filePath);
        }

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string filePath, CancellationToken ct)
    {
        var absolutePath = ToAbsolutePath(filePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            _logger.LogInformation("Fayl silindi: {RelativePath}", filePath);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string filePath) => "/" + filePath.Replace('\\', '/').TrimStart('/');

    /// <summary>
    /// Nisbi yolu wwwroot-a görə həll edir və nəticənin wwwroot-dan kənara çıxmadığını yoxlayır
    /// (path traversal-ın qarşısını alır).
    /// </summary>
    private string ToAbsolutePath(string relativePath)
    {
        var root = Path.GetFullPath(_webRootPath);
        var combined = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Fayl yolu icazə verilən qovluqdan kənardadır.");
        }

        return combined;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();

        // Çox uzun adlar fayl sistemi limitlərini aşmasın deyə kəsilir.
        return cleaned.Length > 120 ? cleaned[^120..] : cleaned;
    }
}
