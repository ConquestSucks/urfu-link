using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Notification.Application.Routing;

public interface INotificationHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task<IReadOnlyList<NotificationDraft>> PrepareAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
