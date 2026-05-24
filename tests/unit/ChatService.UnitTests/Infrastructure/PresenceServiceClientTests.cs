using System.Globalization;
using FluentAssertions;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;

namespace ChatService.UnitTests.Infrastructure;

public sealed class PresenceServiceClientTests
{
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
}
