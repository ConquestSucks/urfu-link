using MediaService.Api.Domain;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Domain.Interfaces;

namespace MediaService.Api.Application.Access;

/// <summary>
/// Resolves the "can this user download this asset?" question without going
/// outside MediaService — all access state is replicated locally as
/// <see cref="MediaAccessGrant"/> rows kept in sync via gRPC + Kafka events.
/// </summary>
public sealed class AccessPolicy(IMediaAccessGrantRepository grantRepository)
{
    public async Task<bool> CanDownloadAsync(MediaAsset asset, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);

        // Soft-deleted / failed / hard-deleted assets are unreachable for everybody.
        if (!asset.IsAccessible) return false;

        // Public assets are downloadable by any authenticated user.
        if (asset.Visibility == Visibility.Public) return true;

        // Owner always wins.
        if (asset.OwnerId == userId) return true;

        // Otherwise we need at least one access grant.
        return await grantRepository.HasAccessAsync(asset.Id, userId, cancellationToken).ConfigureAwait(false);
    }
}
