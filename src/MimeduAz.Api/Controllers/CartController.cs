using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeduAz.Application.Services;
using MimeduAz.Contracts.Carts;
using MimeduAz.Contracts.Common;

namespace MimeduAz.Api.Controllers;

/// <summary>Səbət əməliyyatları. Yalnız daxil olmuş istifadəçilər üçün.</summary>
[ApiController]
[Route("api/v1/cart")]
[Authorize]
[Produces("application/json")]
public sealed class CartController : ControllerBase
{
    private readonly ICartService _cart;

    public CartController(ICartService cart) => _cart = cart;

    /// <summary>Cari səbət və ümumi məbləğ.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct) =>
        Ok(await _cart.GetAsync(ct));

    /// <summary>Səbətə ödənişli resurs və ya təlim əlavə edir.</summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request, CancellationToken ct) =>
        Ok(await _cart.AddItemAsync(request, ct));

    /// <summary>Səbətdən sətri silir.</summary>
    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid id, CancellationToken ct) =>
        Ok(await _cart.RemoveItemAsync(id, ct));
}
