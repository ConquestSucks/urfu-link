using System.Globalization;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;
using PresenceGrpc = Urfu.Link.Services.Presence.Grpc;

namespace ChatService.UnitTests.Infrastructure;

public sealed class PresenceServiceClientTests
{
    [Fact]
    public async Task SetTypingAsync_AddsAuthorizationMetadata_WhenBearerTokenIsAvailable()
    {
        var grpcClient = new StubInternalApiClient();
        var sut = new PresenceServiceClient(
            grpcClient,
            new StubGrpcBearerTokenProvider("service-token"),
            NullLogger<PresenceServiceClient>.Instance);

        await sut.SetTypingAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), isTyping: true, default);

        grpcClient.LastSetTypingHeaders.Should().NotBeNull();
        grpcClient.LastSetTypingHeaders!.Should()
            .ContainSingle(h => h.Key == "authorization" && h.Value == "Bearer service-token");
    }

    [Fact]
    public void MapToConversationGuid_UsesFirstThirtyTwoHexCharsForDirectConversationIds()
    {
        var conversationId = "d39b2933cccdd8b2812a2b8f401fb2a9d9f6abcd";

        var mapped = PresenceServiceClient.MapToConversationGuid(conversationId);

        mapped.ToString("D", CultureInfo.InvariantCulture)
            .Should().Be("d39b2933-cccd-d8b2-812a-2b8f401fb2a9");
    }

    [Fact]
    public void MapToConversationGuid_ExtractsDisciplineId()
    {
        var mapped = PresenceServiceClient.MapToConversationGuid(
            "discipline:11111111111141118111111111111111");

        mapped.ToString("D", CultureInfo.InvariantCulture)
            .Should().Be("11111111-1111-4111-8111-111111111111");
    }

    private sealed class StubInternalApiClient : PresenceGrpc.InternalApi.InternalApiClient
    {
        public Metadata? LastSetTypingHeaders { get; private set; }

        public override AsyncUnaryCall<PresenceGrpc.SetTypingReply> SetTypingAsync(
            PresenceGrpc.SetTypingRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            LastSetTypingHeaders = headers;
            return new AsyncUnaryCall<PresenceGrpc.SetTypingReply>(
                Task.FromResult(new PresenceGrpc.SetTypingReply()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
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
}
