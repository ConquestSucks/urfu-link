using FluentAssertions;
using Grpc.Core;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;
using MediaGrpc = MediaService.Api.Grpc;

namespace Urfu.Link.Services.Chat.UnitTests.Infrastructure;

public sealed class MediaServiceClientTests
{
    [Fact]
    public async Task BatchGetMetadataAsync_AddsAuthorizationMetadata_WhenBearerTokenIsAvailable()
    {
        var assetId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grpcClient = new StubInternalApiClient(assetId, ownerId);
        var sut = new MediaServiceClient(
            grpcClient,
            new StubGrpcBearerTokenProvider("service-token"));

        await sut.BatchGetMetadataAsync(new[] { assetId }, default);

        grpcClient.LastBatchGetMetadataHeaders.Should().NotBeNull();
        grpcClient.LastBatchGetMetadataHeaders!.Should()
            .ContainSingle(h => h.Key == "authorization" && h.Value == "Bearer service-token");
    }

    [Fact]
    public async Task BatchGetMetadataAsync_MapsVoiceDuration_WhenPresent()
    {
        var assetId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grpcClient = new StubInternalApiClient(assetId, ownerId, durationSeconds: 19);
        var sut = new MediaServiceClient(
            grpcClient,
            new StubGrpcBearerTokenProvider("service-token"));

        var result = await sut.BatchGetMetadataAsync(new[] { assetId }, default);

        result.Should().ContainSingle().Which.DurationSeconds.Should().Be(19);
    }

    [Fact]
    public async Task GrantConversationAccessAsync_AddsAuthorizationMetadata_WhenBearerTokenIsAvailable()
    {
        var grpcClient = new StubInternalApiClient(Guid.NewGuid(), Guid.NewGuid());
        var sut = new MediaServiceClient(
            grpcClient,
            new StubGrpcBearerTokenProvider("service-token"));

        await sut.GrantConversationAccessAsync(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            "conversation-1",
            Guid.NewGuid(),
            default);

        grpcClient.LastGrantAssetAccessHeaders.Should().NotBeNull();
        grpcClient.LastGrantAssetAccessHeaders!.Should()
            .ContainSingle(h => h.Key == "authorization" && h.Value == "Bearer service-token");
    }

    private sealed class StubInternalApiClient(Guid assetId, Guid ownerId, int? durationSeconds = null)
        : MediaGrpc.InternalApi.InternalApiClient
    {
        public Metadata? LastBatchGetMetadataHeaders { get; private set; }

        public Metadata? LastGrantAssetAccessHeaders { get; private set; }

        public override AsyncUnaryCall<MediaGrpc.BatchGetMetadataReply> BatchGetMetadataAsync(
            MediaGrpc.BatchGetMetadataRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            LastBatchGetMetadataHeaders = headers;
            var metadata = new MediaGrpc.AssetMetadata
            {
                AssetId = assetId.ToString("D"),
                OwnerId = ownerId.ToString("D"),
                Kind = (int)AttachmentType.Image,
                SizeBytes = 1024,
                MimeType = "image/png",
                OriginalFileName = "asset.png",
                State = MediaGrpc.AssetState.Uploaded,
            };
            if (durationSeconds is { } duration)
            {
                metadata.DurationSeconds = duration;
            }

            return Unary(new MediaGrpc.BatchGetMetadataReply
            {
                Items =
                {
                    metadata,
                },
            });
        }

        public override AsyncUnaryCall<MediaGrpc.GrantAssetAccessReply> GrantAssetAccessAsync(
            MediaGrpc.GrantAssetAccessRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            LastGrantAssetAccessHeaders = headers;
            return Unary(new MediaGrpc.GrantAssetAccessReply());
        }
    }

    private sealed class StubGrpcBearerTokenProvider(string? token) : IGrpcBearerTokenProvider
    {
        public ValueTask<Metadata?> GetAuthorizationMetadataAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return ValueTask.FromResult<Metadata?>(null);
            }

            return ValueTask.FromResult<Metadata?>(new Metadata
            {
                { "authorization", $"Bearer {token}" },
            });
        }
    }

    private static AsyncUnaryCall<T> Unary<T>(T response)
        => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
}
