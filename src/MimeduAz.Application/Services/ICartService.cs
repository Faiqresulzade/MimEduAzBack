using MimeduAz.Contracts.Carts;

namespace MimeduAz.Application.Services;

public interface ICartService
{
    Task<CartDto> GetAsync(CancellationToken ct);
    Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken ct);
    Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct);
}
