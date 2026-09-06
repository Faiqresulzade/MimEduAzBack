using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Blog;
using MimeduAz.Contracts.Common;
using MimeduAz.Domain.Constants;

namespace MimeduAz.Api.Controllers;

/// <summary>Metodik yazılar.</summary>
// Diqqət: sinif səviyyəsində [AllowAnonymous] qoymaq olmaz - o, action-dakı
// [Authorize] atributlarını da ləğv edərdi və admin endpoint-ləri açıq qalardı.
[ApiController]
[Route("api/v1/blog")]
[Produces("application/json")]
public sealed class BlogController : ControllerBase
{
    private readonly IBlogService _blog;

    public BlogController(IBlogService blog) => _blog = blog;

    /// <summary>Bütün blog yazıları (ən yenidən köhnəyə).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<BlogPostDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BlogPostDto>>> Get(CancellationToken ct) =>
        Ok(await _blog.GetAsync(ct));

    /// <summary>Blog yazısının tam mətni.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BlogPostDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlogPostDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _blog.GetByIdAsync(id, ct));

    /// <summary>Yeni blog yazısı əlavə edir. Yalnız Admin.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(BlogPostDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BlogPostDetailDto>> Create(SaveBlogPostRequest request, CancellationToken ct)
    {
        var created = await _blog.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Blog yazısını yeniləyir. Yalnız Admin.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(BlogPostDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlogPostDetailDto>> Update(
        Guid id, SaveBlogPostRequest request, CancellationToken ct) =>
        Ok(await _blog.UpdateAsync(id, request, ct));

    /// <summary>Blog yazısını silir. Yalnız Admin.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _blog.DeleteAsync(id, ct);
        return NoContent();
    }
}
