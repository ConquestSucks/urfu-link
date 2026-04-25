using MediaService.Api.Domain.Enums;
using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace MediaService.Api.Domain.Events;

/// <summary>
/// Published when one or more users lose access to an asset
/// (e.g. they left a conversation, the grant source itself was revoked).
/// </summary>
public sealed record MediaAccessRevokedEvent(
    Guid AssetId,
    IReadOnlyList<Guid> UserIds,
    GrantSource Source,
    string? SourceId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "media.access_revoked.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
