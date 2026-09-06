using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Contracts.Blog;
using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Services;

public sealed class BlogService : IBlogService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<BlogService> _logger;

    public BlogService(IApplicationDbContext db, ILogger<BlogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BlogPostDto>> GetAsync(CancellationToken ct)
    {
        var posts = await _db.BlogPosts
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return posts.Select(p => p.ToDto()).ToList();
    }

    public async Task<BlogPostDetailDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var post = await _db.BlogPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Blog yazısı", id);

        return post.ToDetailDto();
    }

    public async Task<BlogPostDetailDto> CreateAsync(SaveBlogPostRequest request, CancellationToken ct)
    {
        var post = new BlogPost { CreatedAt = DateTime.UtcNow };
        Apply(post, request);

        _db.BlogPosts.Add(post);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Blog yazısı yaradıldı. PostId: {PostId}", post.Id);
        return post.ToDetailDto();
    }

    public async Task<BlogPostDetailDto> UpdateAsync(Guid id, SaveBlogPostRequest request, CancellationToken ct)
    {
        var post = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw NotFoundException.For("Blog yazısı", id);

        Apply(post, request);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Blog yazısı yeniləndi. PostId: {PostId}", post.Id);
        return post.ToDetailDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var post = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw NotFoundException.For("Blog yazısı", id);

        _db.BlogPosts.Remove(post);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Blog yazısı silindi. PostId: {PostId}", id);
    }

    private static void Apply(BlogPost post, SaveBlogPostRequest request)
    {
        post.Title = request.Title.Trim();
        post.Tag = request.Tag.Trim();
        post.ReadTime = request.ReadTime.Trim();
        post.Excerpt = request.Excerpt.Trim();
        post.Body = request.Body
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();
    }
}
