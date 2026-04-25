using MediaService.Api.Domain.Enums;
using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace MediaService.Api.Domain.Events;

/// <summary>
/// Published when one or more users gain access to an asset (via direct grant
/// or because they joined a conversation/discipline that holds a grant).
/// Carries the full delta in a single event for batch efficiency.
/// </summary>
public sealed record MediaAccessGrantedEvent(
    Guid AssetId,
    IReadOnlyList<Guid> UserIds,
    GrantSource Source,
    string? SourceId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "media.access_granted.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
