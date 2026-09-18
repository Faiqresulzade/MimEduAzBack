using MimeduAz.Domain.Entities;

namespace MimeduAz.Application.Services;

/// <summary>
/// Platforma bildirişləri. Bildiriş göndərilə bilməsə heç bir əməliyyat pozulmur —
/// bütün metodlar səssizcə uğursuz olur və yalnız loga yazır.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Yeni resurs moderasiya növbəsinə düşəndə bütün adminlərə məktub göndərir.
    /// </summary>
    Task NotifyAdminsOfPendingResourceAsync(Resource resource, CancellationToken ct);
}
