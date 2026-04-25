using Grpc.Core;
using MediaService.Api.Domain;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Grpc;
using DomainEnums = MediaService.Api.Domain.Enums;

namespace MediaService.Api.Services;

public sealed class InternalApiService(
    IMediaAssetRepository assetRepository,
    IMediaAccessGrantRepository grantRepository) : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "media-service",
            Utc = DateTimeOffset.UtcNow.ToString("O"),
        });
    }

    public override async Task<CheckOwnershipReply> CheckOwnership(
        CheckOwnershipRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.AssetId, out var assetId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad guid in request."));
        }

        var asset = await assetRepository.GetByIdAsync(assetId, context.CancellationToken).ConfigureAwait(false);
        return new CheckOwnershipReply
        {
            Exists = asset is not null,
            IsOwner = asset is not null && asset.OwnerId == userId,
        };
    }

    public override async Task<BatchGetMetadataReply> BatchGetMetadata(
        BatchGetMetadataRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var assetIds = request.AssetIds
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        var assets = await assetRepository.GetByIdsAsync(assetIds, context.CancellationToken).ConfigureAwait(false);

        var reply = new BatchGetMetadataReply();
        foreach (var asset in assets)
        {
            reply.Items.Add(new AssetMetadata
            {
                AssetId = asset.Id.ToString(),
                OwnerId = asset.OwnerId.ToString(),
                Visibility = (int)asset.Visibility,
                Kind = (int)asset.Kind,
                SizeBytes = asset.Size,
                MimeType = asset.MimeType,
                OriginalFileName = asset.OriginalFileName,
                State = asset.State.ToString(),
                CreatedAtUtc = asset.CreatedAtUtc.ToString("O"),
            });
        }
        return reply;
    }

    public override async Task<GrantAssetAccessReply> GrantAssetAccess(
        GrantAssetAccessRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.AssetId, out var assetId)
            || !Guid.TryParse(request.GrantedByUserId, out var grantedBy))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad guid in request."));
        }

        var source = (DomainEnums.GrantSource)request.Source;
        var sourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId;
        var userIds = ParseUserIdsOrThrow(request.UserIds);

        var grants = userIds
            .Select(uid => MediaAccessGrant.Create(assetId, uid, source, sourceId, grantedBy))
            .ToList();

        await grantRepository.AddRangeAsync(grants, context.CancellationToken).ConfigureAwait(false);
        await grantRepository.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        return new GrantAssetAccessReply { GrantsAdded = grants.Count };
    }

    public override async Task<RevokeAssetAccessReply> RevokeAssetAccess(
        RevokeAssetAccessRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.AssetId, out var assetId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad guid in request."));
        }

        var source = (DomainEnums.GrantSource)request.Source;
        var sourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId;
        var userIds = ParseUserIdsOrThrow(request.UserIds);

        var removed = await grantRepository
            .RemoveRangeAsync(assetId, userIds, source, sourceId, context.CancellationToken)
            .ConfigureAwait(false);

        return new RevokeAssetAccessReply { GrantsRemoved = removed };
    }

    private static List<Guid> ParseUserIdsOrThrow(IEnumerable<string> userIds)
    {
        var result = new List<Guid>();
        foreach (var raw in userIds)
        {
            if (!Guid.TryParse(raw, out var parsed))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad guid in user_ids."));
            }
            result.Add(parsed);
        }
        return result.Distinct().ToList();
    }

    public override async Task<RevokeAllForSourceReply> RevokeAllForSource(
        RevokeAllForSourceRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(request.SourceId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "source_id is required."));
        }

        var source = (DomainEnums.GrantSource)request.Source;
        var removed = await grantRepository
            .RemoveAllForSourceAsync(source, request.SourceId, context.CancellationToken)
            .ConfigureAwait(false);

        return new RevokeAllForSourceReply { GrantsRemoved = removed };
    }
}
