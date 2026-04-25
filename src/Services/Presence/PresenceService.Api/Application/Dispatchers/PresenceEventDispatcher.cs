using System.Diagnostics;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Presence.Domain;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;

namespace Urfu.Link.Services.Presence.Application.Dispatchers;

/// <summary>
/// Publishes <see cref="UserCameOnlineEvent"/> directly through the outbox
/// because there is no EF aggregate to attach the event to (the session lives
/// only in Redis). <see cref="UserWentOfflineEvent"/> is published differently
/// — through <c>LastSeen</c> aggregate domain events on save — to guarantee
/// PG-row + outbox atomicity.
/// </summary>
public sealed class PresenceEventDispatcher(IOutboxWriter outboxWriter, ServiceProfile descriptor)
{
    public Task PublishUserCameOnlineAsync(
        Guid userId,
        IReadOnlyList<Platform> platforms,
        CancellationToken cancellationToken)
    {
        var ev = new UserCameOnlineEvent(userId, platforms, DateTimeOffset.UtcNow);
        var envelope = new IntegrationEnvelope<UserCameOnlineEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: descriptor.ServiceName,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: ev);
        return outboxWriter.EnqueueAsync(KafkaTopicNames.PresenceEvents, envelope, cancellationToken).AsTask();
    }
}
