using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Application.Routing;

namespace Urfu.Link.Services.Notification.Messaging;

internal static class RoutingDispatcher
{
    public static Task Route<TEvent>(
        IServiceProvider scope,
        INotificationHandler<TEvent> handler,
        TEvent payload,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var router = scope.GetRequiredService<NotificationRouter>();
        return router.RouteAsync(payload, handler, cancellationToken);
    }
}
