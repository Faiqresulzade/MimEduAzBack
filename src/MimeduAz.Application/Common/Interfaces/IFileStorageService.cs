namespace MimeduAz.Application.Common.Interfaces;

/// <summary>
/// Fayl saxlama abstraksiyası. Hazırda local disk implementasiyası var,
/// gələcəkdə S3/Azure Blob implementasiyası bu interfeysi əvəz edə bilər.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Faylı saxlayır və sonradan oxumaq üçün nisbi yolu qaytarır.</summary>
    Task<string> SaveAsync(Stream fileStream, string fileName, CancellationToken ct);

    Task<Stream> GetAsync(string filePath, CancellationToken ct);

    Task DeleteAsync(string filePath, CancellationToken ct);

    /// <summary>Faylın publik olaraq əlçatan URL-i (local rejimdə statik fayl yolu).</summary>
    string GetPublicUrl(string filePath);
}
