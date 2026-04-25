using FluentAssertions;
using global::Grpc.Net.Client;
using MediaService.Api.Grpc;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Grpc;

[Collection(IntegrationCollection.Name)]
public sealed class InternalApiGrpcTests : IClassFixture<MediaServiceFactory>, IDisposable
{
    private readonly MediaServiceFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly GrpcChannel _channel;

    public InternalApiGrpcTests(MediaServiceFactory factory)
    {
        _factory = factory;
        _factory.ResetCapturedState();
        _httpClient = _factory.CreateClient();
        _channel = GrpcChannel.ForAddress(_httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = _httpClient });
    }

    private InternalApi.InternalApiClient AuthorizedClient(Guid userId)
    {
        TestAuthHandler.CurrentPrincipal = TestAssetBuilder.MakeUser(userId);
        return new InternalApi.InternalApiClient(_channel);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _httpClient.Dispose();
    }

    [Fact]
    public async Task Ping_ReturnsPongPlusEcho()
    {
        var client = AuthorizedClient(Guid.NewGuid());

        var reply = await client.PingAsync(new PingRequest { Message = "hello" });

        reply.Service.Should().Be("media-service");
        reply.Message.Should().Be("pong:hello");
    }

    [Fact]
    public async Task CheckOwnership_OwnersAsset_ReturnsTrueAndExists()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var client = AuthorizedClient(ownerId);
        var reply = await client.CheckOwnershipAsync(new CheckOwnershipRequest
        {
            AssetId = assetId.ToString(),
            UserId = ownerId.ToString(),
        });

        reply.Exists.Should().BeTrue();
        reply.IsOwner.Should().BeTrue();
    }

    [Fact]
    public async Task CheckOwnership_OtherUsersAsset_ReturnsExistsButNotOwner()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var client = AuthorizedClient(Guid.NewGuid());
        var reply = await client.CheckOwnershipAsync(new CheckOwnershipRequest
        {
            AssetId = assetId.ToString(),
            UserId = Guid.NewGuid().ToString(),
        });

        reply.Exists.Should().BeTrue();
        reply.IsOwner.Should().BeFalse();
    }

    [Fact]
    public async Task CheckOwnership_MissingAsset_ReturnsExistsFalse()
    {
        var client = AuthorizedClient(Guid.NewGuid());

        var reply = await client.CheckOwnershipAsync(new CheckOwnershipRequest
        {
            AssetId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid().ToString(),
        });

        reply.Exists.Should().BeFalse();
        reply.IsOwner.Should().BeFalse();
    }

    [Fact]
    public async Task BatchGetMetadata_DropsInvalidIdsAndReturnsKnownAssets()
    {
        var ownerId = Guid.NewGuid();
        var assetId1 = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
        var assetId2 = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var client = AuthorizedClient(ownerId);
        var request = new BatchGetMetadataRequest();
        request.AssetIds.Add(assetId1.ToString());
        request.AssetIds.Add("not-a-guid");
        request.AssetIds.Add(assetId2.ToString());

        var reply = await client.BatchGetMetadataAsync(request);

        reply.Items.Select(i => i.AssetId).Should()
            .BeEquivalentTo([assetId1.ToString(), assetId2.ToString()]);
    }

    [Fact]
    public async Task GrantAssetAccess_AddsOneRowPerUser()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var client = AuthorizedClient(ownerId);
        var request = new GrantAssetAccessRequest
        {
            AssetId = assetId.ToString(),
            GrantedByUserId = ownerId.ToString(),
            Source = global::MediaService.Api.Grpc.GrantSource.Direct,
        };
        request.UserIds.Add(Guid.NewGuid().ToString());
        request.UserIds.Add(Guid.NewGuid().ToString());

        var reply = await client.GrantAssetAccessAsync(request);

        reply.GrantsAdded.Should().Be(2);
    }

    [Fact]
    public async Task RevokeAllForSource_RemovesEverythingForThatSource()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
        const string conversationId = "conv-1";

        var client = AuthorizedClient(ownerId);
        var grant = new GrantAssetAccessRequest
        {
            AssetId = assetId.ToString(),
            GrantedByUserId = ownerId.ToString(),
            Source = global::MediaService.Api.Grpc.GrantSource.Conversation,
            SourceId = conversationId,
        };
        grant.UserIds.Add(Guid.NewGuid().ToString());
        grant.UserIds.Add(Guid.NewGuid().ToString());
        await client.GrantAssetAccessAsync(grant);

        var reply = await client.RevokeAllForSourceAsync(new RevokeAllForSourceRequest
        {
            Source = global::MediaService.Api.Grpc.GrantSource.Conversation,
            SourceId = conversationId,
        });

        reply.GrantsRemoved.Should().Be(2);
    }
}
