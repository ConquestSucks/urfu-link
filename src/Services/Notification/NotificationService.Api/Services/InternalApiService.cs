using System.Globalization;
using Grpc.Core;
using Urfu.Link.Services.Notification.Application.Direct;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Grpc;

namespace Urfu.Link.Services.Notification.Services;

public sealed class InternalApiService(
    NotificationRouter router,
    DirectNotificationHandler handler,
    BadgeService badgeService) : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = context;
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "notification-service",
            Utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        });
    }

    public override async Task<SendDirectNotificationReply> SendDirectNotification(
        SendDirectNotificationRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var userId = ParseGuid(request.UserId, nameof(request.UserId));
        if (!Enum.IsDefined((NotificationCategory)request.Category))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unknown category {request.Category}"));
        }

        var sourceId = string.IsNullOrWhiteSpace(request.SourceId)
            ? Guid.NewGuid()
            : ParseGuid(request.SourceId, nameof(request.SourceId));

        var command = new DirectNotificationCommand(
            RecipientUserId: userId,
            Category: (NotificationCategory)request.Category,
            Severity: (NotificationSeverity)request.Severity,
            Title: request.Title,
            Body: request.Body,
            DeepLink: string.IsNullOrWhiteSpace(request.DeepLink) ? null : request.DeepLink,
            Data: request.Data.ToDictionary(kv => kv.Key, kv => kv.Value),
            SourceId: sourceId,
            SourceEventType: string.IsNullOrWhiteSpace(request.SourceEventType)
                ? "notification.direct.v1"
                : request.SourceEventType);

        var outcome = await router.RouteAsync(command, handler, context.CancellationToken).ConfigureAwait(false);

        return new SendDirectNotificationReply
        {
            NotificationId = sourceId.ToString("N", CultureInfo.InvariantCulture),
            Created = outcome.Created > 0,
        };
    }

    public override async Task<GetBadgeCountReply> GetBadgeCount(GetBadgeCountRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var userId = ParseGuid(request.UserId, nameof(request.UserId));
        var snapshot = await badgeService.GetSnapshotAsync(userId, context.CancellationToken).ConfigureAwait(false);

        var reply = new GetBadgeCountReply { Total = snapshot.Total };
        foreach (var (category, count) in snapshot.PerCategory)
        {
            reply.PerCategory.Add(category, count);
        }

        return reply;
    }

    private static Guid ParseGuid(string raw, string parameterName)
    {
        if (!Guid.TryParse(raw, out var parsed) || parsed == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{parameterName} must be a non-empty GUID."));
        }

        return parsed;
    }
}
