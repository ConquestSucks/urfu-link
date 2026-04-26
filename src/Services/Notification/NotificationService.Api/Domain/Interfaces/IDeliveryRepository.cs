using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Interfaces;

public interface IDeliveryRepository
{
    Task<IReadOnlyList<Delivery>> LeasePendingBatchAsync(
        DeliveryChannel channel,
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
