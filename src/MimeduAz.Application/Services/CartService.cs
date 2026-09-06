using Microsoft.EntityFrameworkCore;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Contracts.Carts;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class CartService : ICartService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CartService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CartDto> GetAsync(CancellationToken ct)
    {
        var cart = await GetOrCreateCartAsync(_currentUser.RequireUserId(), ct);
        return await ToDtoAsync(cart, ct);
    }

    public async Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var cart = await GetOrCreateCartAsync(userId, ct);

        var price = request.ItemType switch
        {
            CatalogItemType.Resource => await ResolveResourcePriceAsync(request.ItemId, userId, ct),
            CatalogItemType.Training => await ResolveTrainingPriceAsync(request.ItemId, userId, ct),
            _ => throw new BadRequestException("Naməlum məhsul növü.")
        };

        if (cart.Items.Any(i => i.ItemType == request.ItemType && i.ItemId == request.ItemId))
        {
            throw new ConflictException("Bu məhsul artıq səbətdədir.");
        }

        var item = new CartItem
        {
            CartId = cart.Id,
            ItemType = request.ItemType,
            ItemId = request.ItemId,
            Price = price,
            AddedAt = DateTime.UtcNow
        };

        // Açıq şəkildə DbSet-ə əlavə edirik: Id əvvəlcədən doldurulduğu üçün
        // yalnız naviqasiya kolleksiyasına atsaq EF onu "Modified" kimi izləyər.
        // cart.Items relationship fixup ilə avtomatik yenilənir.
        _db.CartItems.Add(item);

        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(cart, ct);
    }

    public async Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var cart = await GetOrCreateCartAsync(userId, ct);

        var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId)
                   ?? throw NotFoundException.For("Səbət sətri", cartItemId);

        _db.CartItems.Remove(item);
        cart.Items.Remove(item);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(cart, ct);
    }

    private async Task<Cart> GetOrCreateCartAsync(Guid userId, CancellationToken ct)
    {
        var cart = await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is not null)
        {
            return cart;
        }

        cart = new Cart { UserId = userId, CreatedAt = DateTime.UtcNow };
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync(ct);
        return cart;
    }

    private async Task<decimal> ResolveResourcePriceAsync(Guid resourceId, Guid userId, CancellationToken ct)
    {
        var resource = await _db.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct)
            ?? throw NotFoundException.For("Resurs", resourceId);

        if (resource.Status != ResourceStatus.Approved)
        {
            throw new BadRequestException("Bu resurs hələ təsdiqlənməyib.");
        }

        if (!resource.IsPaid)
        {
            throw new BadRequestException("Pulsuz resursu birbaşa endirə bilərsiniz, səbətə əlavə etməyə ehtiyac yoxdur.");
        }

        if (resource.AuthorId == userId)
        {
            throw new BadRequestException("Öz resursunuzu satın ala bilməzsiniz.");
        }

        var alreadyBought = await _db.OrderItems.AnyAsync(oi =>
            oi.ItemType == CatalogItemType.Resource &&
            oi.ItemId == resourceId &&
            oi.Order!.UserId == userId &&
            oi.Order.Status == OrderStatus.Paid, ct);

        if (alreadyBought)
        {
            throw new ConflictException("Bu resursu artıq almısınız.");
        }

        return resource.Price;
    }

    private async Task<decimal> ResolveTrainingPriceAsync(Guid trainingId, Guid userId, CancellationToken ct)
    {
        var training = await _db.Trainings
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trainingId, ct)
            ?? throw NotFoundException.For("Təlim", trainingId);

        var alreadyEnrolled = await _db.Enrollments
            .AnyAsync(e => e.TrainingId == trainingId && e.UserId == userId, ct);

        if (alreadyEnrolled)
        {
            throw new ConflictException("Bu təlimə artıq yazılmısınız.");
        }

        if (training.SeatLimit is not null)
        {
            var seatsTaken = await _db.Enrollments.CountAsync(e => e.TrainingId == trainingId, ct);
            if (seatsTaken >= training.SeatLimit.Value)
            {
                throw new ConflictException("Bu təlimdə boş yer qalmayıb.");
            }
        }

        return training.Price;
    }

    private async Task<CartDto> ToDtoAsync(Cart cart, CancellationToken ct)
    {
        var items = cart.Items.OrderBy(i => i.AddedAt).ToList();

        var resourceIds = items.Where(i => i.ItemType == CatalogItemType.Resource).Select(i => i.ItemId).ToList();
        var trainingIds = items.Where(i => i.ItemType == CatalogItemType.Training).Select(i => i.ItemId).ToList();

        var resourceNames = await _db.Resources
            .AsNoTracking()
            .Where(r => resourceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var trainingNames = await _db.Trainings
            .AsNoTracking()
            .Where(t => trainingIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var dtos = items.Select(i => new CartItemDto(
            i.Id,
            i.ItemType,
            i.ItemId,
            i.ItemType == CatalogItemType.Resource
                ? resourceNames.GetValueOrDefault(i.ItemId, "Silinmiş resurs")
                : trainingNames.GetValueOrDefault(i.ItemId, "Silinmiş təlim"),
            i.Price,
            i.AddedAt)).ToList();

        return new CartDto(cart.Id, dtos, dtos.Sum(d => d.Price));
    }
}
