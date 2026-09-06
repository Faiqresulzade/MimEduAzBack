namespace MimeduAz.Contracts.Blog;

public sealed record BlogPostDto(
    Guid Id,
    string Title,
    string Tag,
    string ReadTime,
    string Excerpt,
    DateTime CreatedAt);

public sealed record BlogPostDetailDto(
    Guid Id,
    string Title,
    string Tag,
    string ReadTime,
    string Excerpt,
    IReadOnlyList<string> Body,
    DateTime CreatedAt);

/// <summary>Blog yazısının yaradılması/redaktəsi (yalnız Admin).</summary>
public sealed class SaveBlogPostRequest
{
    public string Title { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;

    /// <summary>Oxunma müddəti etiketi, məs. "6 dəq".</summary>
    public string ReadTime { get; set; } = string.Empty;

    public string Excerpt { get; set; } = string.Empty;

    /// <summary>Yazının paraqrafları.</summary>
    public List<string> Body { get; set; } = new();
}
