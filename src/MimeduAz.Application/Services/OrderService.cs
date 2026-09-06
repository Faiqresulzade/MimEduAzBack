using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Application.Common.Utilities;
using MimeduAz.Contracts.Orders;
using MimeduAz.Domain.Entities;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly CommissionOptions _commission;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IOptions<CommissionOptions> commission,
        ILogger<OrderService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _commission = commission.Value;
        _logger = logger;
    }

    public async Task<OrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var cart = await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is null || cart.Items.Count == 0)
        {
            throw new BadRequestException("Səbət boşdur.");
        }

        var items = cart.Items.OrderBy(i => i.AddedAt).ToList();

        var resourceIds = items.Where(i => i.ItemType == CatalogItemType.Resource).Select(i => i.ItemId).ToList();
        var trainingIds = items.Where(i => i.ItemType == CatalogItemType.Training).Select(i => i.ItemId).ToList();

        var resources = await _db.Resources
            .Where(r => resourceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var trainings = await _db.Trainings
            .Where(t => trainingIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var order = new Order
        {
            Code = await GenerateOrderCodeAsync(ct),
            UserId = userId,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
                ? user.FullName
                : request.CustomerName.Trim(),
            Status = OrderStatus.Paid,
            CreatedAt = DateTime.UtcNow
        };

        var existingEnrollments = await _db.Enrollments
            .Where(e => e.UserId == userId && trainingIds.Contains(e.TrainingId))
            .Select(e => e.TrainingId)
            .ToListAsync(ct);

        foreach (var item in items)
        {
            if (item.ItemType == CatalogItemType.Resource)
            {
                if (!resources.TryGetValue(item.ItemId, out var resource))
                {
                    throw new BadRequestException("Səbətdəki resurslardan biri artıq mövcud deyil.");
                }

                // Ödənişli resursdan platforma komissiya tutur, qalanı müəllifə qalır.
                var commission = Math.Round(item.Price * _commission.ResourcePercent, 2, MidpointRounding.AwayFromZero);

                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ItemType = CatalogItemType.Resource,
                    ItemId = resource.Id,
                    Name = resource.Name,
                    Price = item.Price,
                    CommissionAmount = commission,
                    AuthorPayoutAmount = item.Price - commission
                });
            }
            else
            {
                if (!trainings.TryGetValue(item.ItemId, out var training))
                {
                    throw new BadRequestException("Səbətdəki təlimlərdən biri artıq mövcud deyil.");
                }

                if (training.SeatLimit is not null)
                {
                    var seatsTaken = await _db.Enrollments.CountAsync(e => e.TrainingId == training.Id, ct);
                    if (seatsTaken >= training.SeatLimit.Value)
                    {
                        throw new ConflictException($"«{training.Name}» təlimində boş yer qalmayıb.");
                    }
                }

                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ItemType = CatalogItemType.Training,
                    ItemId = training.Id,
                    Name = training.Name,
                    Price = item.Price,
                    // Təlim platformanın öz məhsuludur - müəllif payı yoxdur.
                    CommissionAmount = 0m,
                    AuthorPayoutAmount = 0m
                });

                if (!existingEnrollments.Contains(training.Id))
                {
                    _db.Enrollments.Add(new Enrollment
                    {
                        UserId = userId,
                        TrainingId = training.Id,
                        ProgressPercent = 0,
                        Status = EnrollmentStatus.InProgress,
                        EnrolledAt = DateTime.UtcNow
                    });
                    existingEnrollments.Add(training.Id);
                }
            }
        }

        order.TotalAmount = order.Items.Sum(i => i.Price);
        order.CommissionAmount = order.Items.Sum(i => i.CommissionAmount);

        _db.Orders.Add(order);

        // Səbət boşaldılır - sifariş və enrollment-lərlə birlikdə tək SaveChanges-də atomik yazılır.
        _db.CartItems.RemoveRange(items);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sifariş tamamlandı (demo ödəniş). OrderId: {OrderId}, Sətir sayı: {ItemCount}",
            order.Id, order.Items.Count);

        return order.ToDto();
    }

    public async Task<IReadOnlyList<OrderDto>> GetMineAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(o => o.ToDto()).ToList();
    }

    private Task<string> GenerateOrderCodeAsync(CancellationToken ct) =>
        CodeFactory.UniqueAsync(
            CodeFactory.NewOrderCode,
            (code, token) => _db.Orders.AnyAsync(o => o.Code == code, token),
            CodeFactory.NewOrderCodeLong,
            ct);
}
