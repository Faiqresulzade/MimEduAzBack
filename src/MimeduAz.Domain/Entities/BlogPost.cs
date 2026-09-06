namespace MimeduAz.Domain.Entities;

/// <summary>Metodik blog yazısı.</summary>
public class BlogPost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;

    /// <summary>Oxunma müddəti etiketi, məs. "6 dəq".</summary>
    public string ReadTime { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    /// <summary>Paraqraflar. jsonb sütununda saxlanılır.</summary>
    public List<string> Body { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
