using MimeduAz.Contracts.Admin;
using MimeduAz.Contracts.Orders;
using MimeduAz.Contracts.Resources;

namespace MimeduAz.Application.Services;

public interface IAdminService
{
    Task<IReadOnlyList<ResourceDto>> GetPendingResourcesAsync(CancellationToken ct);
    Task<ResourceDetailDto> ApproveResourceAsync(Guid resourceId, CancellationToken ct);
    Task<ResourceDetailDto> RejectResourceAsync(Guid resourceId, RejectResourceRequest request, CancellationToken ct);
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken ct);
    Task<SalesSummaryDto> GetSalesAsync(CancellationToken ct);
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct);
}
