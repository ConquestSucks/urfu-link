using FluentAssertions;
using Grpc.Core;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Grpc;
using MediaService.Api.Services;
using NSubstitute;

namespace MediaService.UnitTests.Unit;

public class InternalApiServiceTests
{
    [Fact]
    public async Task GrantAssetAccess_BadGuidInUserIds_ThrowsInvalidArgument()
    {
        var assetRepo = Substitute.For<IMediaAssetRepository>();
        var grantRepo = Substitute.For<IMediaAccessGrantRepository>();
        var sut = new InternalApiService(assetRepo, grantRepo);

        var request = new GrantAssetAccessRequest
        {
            AssetId = Guid.NewGuid().ToString(),
            GrantedByUserId = Guid.NewGuid().ToString(),
            Source = GrantSource.Direct,
        };
        request.UserIds.Add("not-a-guid");

        var act = async () => await sut.GrantAssetAccess(request, new StubServerCallContext());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task RevokeAssetAccess_BadGuidInUserIds_ThrowsInvalidArgument()
    {
        var assetRepo = Substitute.For<IMediaAssetRepository>();
        var grantRepo = Substitute.For<IMediaAccessGrantRepository>();
        var sut = new InternalApiService(assetRepo, grantRepo);

        var request = new RevokeAssetAccessRequest
        {
            AssetId = Guid.NewGuid().ToString(),
            Source = GrantSource.Direct,
        };
        request.UserIds.Add("also-not-a-guid");

        var act = async () => await sut.RevokeAssetAccess(request, new StubServerCallContext());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task GrantAssetAccess_EmptyUserIds_ThrowsInvalidArgument()
    {
        var assetRepo = Substitute.For<IMediaAssetRepository>();
        var grantRepo = Substitute.For<IMediaAccessGrantRepository>();
        var sut = new InternalApiService(assetRepo, grantRepo);

        var request = new GrantAssetAccessRequest
        {
            AssetId = Guid.NewGuid().ToString(),
            GrantedByUserId = Guid.NewGuid().ToString(),
            Source = GrantSource.Direct,
        };

        var act = async () => await sut.GrantAssetAccess(request, new StubServerCallContext());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task RevokeAssetAccess_EmptyUserIds_ThrowsInvalidArgument()
    {
        var assetRepo = Substitute.For<IMediaAssetRepository>();
        var grantRepo = Substitute.For<IMediaAccessGrantRepository>();
        var sut = new InternalApiService(assetRepo, grantRepo);

        var request = new RevokeAssetAccessRequest
        {
            AssetId = Guid.NewGuid().ToString(),
            Source = GrantSource.Direct,
        };

        var act = async () => await sut.RevokeAssetAccess(request, new StubServerCallContext());

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.Status.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    private sealed class StubServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "Stub";
        protected override string HostCore => "stub";
        protected override string PeerCore => "stub";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddSeconds(30);
        protected override Metadata RequestHeadersCore { get; } = [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override Status StatusCore { get; set; } = Status.DefaultSuccess;
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore { get; } = new(string.Empty, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }
}
