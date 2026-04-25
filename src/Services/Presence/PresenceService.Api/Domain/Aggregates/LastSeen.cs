using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Events;

namespace Urfu.Link.Services.Presence.Domain.Aggregates;

public sealed class LastSeen
{
    private readonly List<IIntegrationEvent> _domainEvents = [];

    public Guid UserId { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public Platform LastPlatform { get; private set; }

    public IReadOnlyList<IIntegrationEvent> DomainEvents => _domainEvents;

    private LastSeen() { }

    public static LastSeen Create(Guid userId, Platform platform, DateTimeOffset utcNow)
    {
        return new LastSeen
        {
            UserId = userId,
            LastPlatform = platform,
            LastSeenAt = utcNow,
        };
    }

    public void Update(Platform platform, DateTimeOffset utcNow)
    {
        LastSeenAt = utcNow;
        LastPlatform = platform;
        _domainEvents.Add(new UserWentOfflineEvent(UserId, utcNow, utcNow));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
