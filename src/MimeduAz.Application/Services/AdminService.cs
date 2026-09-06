using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeduAz.Application.Common.Exceptions;
using MimeduAz.Application.Common.Interfaces;
using MimeduAz.Application.Common.Mappings;
using MimeduAz.Application.Common.Options;
using MimeduAz.Contracts.Admin;
using MimeduAz.Contracts.Orders;
using MimeduAz.Contracts.Resources;
using MimeduAz.Domain.Enums;

namespace MimeduAz.Application.Services;

public sealed class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly CommissionOptions _commission;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IApplicationDbContext db,
        IOptions<CommissionOptions> commission,
        ILogger<AdminService> logger)
    {
        _db = db;
        _commission = commission.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ResourceDto>> GetPendingResourcesAsync(CancellationToken ct)
    {
        var resources = await _db.Resources
            .AsNoTracking()
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .Where(r => r.Status == ResourceStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return resources.Select(r => r.ToDto()).ToList();
    }

    public async Task<ResourceDetailDto> ApproveResourceAsync(Guid resourceId, CancellationToken ct)
    {
        var resource = await LoadForModerationAsync(resourceId, ct);

        resource.Status = ResourceStatus.Approved;
        resource.ApprovedAt = DateTime.UtcNow;
        resource.RejectionReason = null;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Resurs təsdiqləndi. ResourceId: {ResourceId}", resourceId);

        return resource.ToDetailDto();
    }

    public async Task<ResourceDetailDto> RejectResourceAsync(
        Guid resourceId, RejectResourceRequest request, CancellationToken ct)
    {
        var resource = await LoadForModerationAsync(resourceId, ct);

        resource.Status = ResourceStatus.Rejected;
        resource.ApprovedAt = null;
        resource.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Resurs rədd edildi. ResourceId: {ResourceId}", resourceId);

        return resource.ToDetailDto();
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();

        // Rolları tək sorğuda çəkirik - istifadəçi başına ayrıca sorğu (N+1) olmasın deyə.
        var roleRows = await _db.UserRoles
            .AsNoTracking()
            .Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name })
            .ToListAsync(ct);

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName ?? string.Empty).ToList());

        var resourceStats = await _db.Resources
            .AsNoTracking()
            .Where(r => userIds.Contains(r.AuthorId))
            .GroupBy(r => r.AuthorId)
            .Select(g => new
            {
                AuthorId = g.Key,
                Total = g.Count(),
                Approved = g.Count(r => r.Status == ResourceStatus.Approved),
                Downloads = g.Sum(r => r.Downloads)
            })
            .ToListAsync(ct);

        var statsByUser = resourceStats.ToDictionary(x => x.AuthorId);

        var enrollmentCounts = await _db.Enrollments
            .AsNoTracking()
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var certificateCounts = await _db.Certificates
            .AsNoTracking()
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        return users.Select(u =>
        {
            var stats = statsByUser.GetValueOrDefault(u.Id);
            return new AdminUserDto(
                u.Id,
                u.FullName,
                u.Email ?? string.Empty,
                u.Subject,
                rolesByUser.GetValueOrDefault(u.Id, Array.Empty<string>()),
                stats?.Total ?? 0,
                stats?.Approved ?? 0,
                stats?.Downloads ?? 0,
                enrollmentCounts.GetValueOrDefault(u.Id),
                certificateCounts.GetValueOrDefault(u.Id),
                u.CreatedAt);
        }).ToList();
    }

    public async Task<SalesSummaryDto> GetSalesAsync(CancellationToken ct)
    {
        var paidItems = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order!.Status == OrderStatus.Paid)
            .ToListAsync(ct);

        var orderCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Paid, ct);

        return new SalesSummaryDto(
            paidItems.Sum(i => i.Price),
            paidItems.Sum(i => i.CommissionAmount),
            paidItems.Sum(i => i.AuthorPayoutAmount),
            orderCount,
            paidItems.Count,
            paidItems.Where(i => i.ItemType == CatalogItemType.Resource).Sum(i => i.Price),
            paidItems.Where(i => i.ItemType == CatalogItemType.Training).Sum(i => i.Price),
            _commission.ResourcePercent);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return orders.Select(o => o.ToDto()).ToList();
    }

    private async Task<Domain.Entities.Resource> LoadForModerationAsync(Guid resourceId, CancellationToken ct) =>
        await _db.Resources
            .Include(r => r.Author)
            .Include(r => r.Quiz)
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct)
        ?? throw NotFoundException.For("Resurs", resourceId);
}
