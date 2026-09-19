namespace MimeduAz.Application.Common.Options;

/// <summary>Fayl yükləmə limitləri və saxlama yolu. appsettings.json -> "FileStorage".</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>wwwroot-a nisbətən kök qovluq.</summary>
    public string RootPath { get; set; } = "uploads/resources";

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = { ".pdf", ".docx", ".pptx" };

    /// <summary>Sınaq sual şəkilləri üçün qovluq (wwwroot-a nisbətən).</summary>
    public string ImageRootPath { get; set; } = "uploads/exams";

    public long MaxImageSizeBytes { get; set; } = 5 * 1024 * 1024;

    public string[] AllowedImageExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".webp" };
}
