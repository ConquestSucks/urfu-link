using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Domain;

/// <summary>
/// Concrete (asset, user) access entry. Group grants (Conversation/Discipline) are
/// expanded into a row per member at the time of creation; subsequent membership
/// changes are pushed by the source service via gRPC and Kafka events.
/// </summary>
public sealed class MediaAccessGrant
{
    public Guid Id { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid UserId { get; private set; }
    public GrantSource Source { get; private set; }
    public string? SourceId { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }

    private MediaAccessGrant() { }

    public static MediaAccessGrant Create(
        Guid assetId,
        Guid userId,
        GrantSource source,
        string? sourceId,
        Guid grantedByUserId)
    {
        if ((source == GrantSource.Conversation || source == GrantSource.Discipline)
            && string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException(
                "SourceId is required when source is Conversation or Discipline.", nameof(sourceId));
        }

        if (source == GrantSource.Direct && !string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("SourceId must be null for Direct grants.", nameof(sourceId));
        }

        return new MediaAccessGrant
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            UserId = userId,
            Source = source,
            SourceId = sourceId,
            GrantedByUserId = grantedByUserId,
            GrantedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
