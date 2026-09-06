using MimeduAz.Contracts.Blog;

namespace MimeduAz.Application.Services;

public interface IBlogService
{
    Task<IReadOnlyList<BlogPostDto>> GetAsync(CancellationToken ct);
    Task<BlogPostDetailDto> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Yeni yazı əlavə edir. Yalnız Admin.</summary>
    Task<BlogPostDetailDto> CreateAsync(SaveBlogPostRequest request, CancellationToken ct);

    /// <summary>Mövcud yazını yeniləyir. Yalnız Admin.</summary>
    Task<BlogPostDetailDto> UpdateAsync(Guid id, SaveBlogPostRequest request, CancellationToken ct);

    /// <summary>Yazını silir. Yalnız Admin.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
