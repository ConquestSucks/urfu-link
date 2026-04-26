using FluentAssertions;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class CursorCodecTests
{
    [Fact]
    public void EncodeDecode_Conversation_RoundTrips()
    {
        var original = new ConversationCursor(
            new DateTimeOffset(2026, 04, 25, 10, 00, 00, TimeSpan.Zero),
            "abc123");

        var encoded = CursorCodec.EncodeConversation(original);
        var decoded = CursorCodec.DecodeConversation(encoded);

        decoded.Should().Be(original);
    }

    [Fact]
    public void EncodeDecode_Message_RoundTrips()
    {
        var original = new MessageCursor(
            new DateTimeOffset(2026, 04, 25, 10, 00, 00, TimeSpan.Zero),
            Guid.NewGuid());

        var encoded = CursorCodec.EncodeMessage(original);
        var decoded = CursorCodec.DecodeMessage(encoded);

        decoded.Should().Be(original);
    }

    [Fact]
    public void EncodeDecode_ThreadActivity_RoundTrips()
    {
        var original = new ThreadActivityCursor(
            new DateTimeOffset(2026, 04, 25, 10, 00, 00, TimeSpan.Zero),
            Guid.NewGuid());

        var encoded = CursorCodec.EncodeThreadActivity(original);
        var decoded = CursorCodec.DecodeThreadActivity(encoded);

        decoded.Should().Be(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Decode_NullOrEmpty_ReturnsNull(string? input)
    {
        CursorCodec.DecodeConversation(input).Should().BeNull();
        CursorCodec.DecodeMessage(input).Should().BeNull();
        CursorCodec.DecodeThreadActivity(input).Should().BeNull();
    }

    [Fact]
    public void Decode_Garbage_ThrowsInvalidChatCursorException()
    {
        var act = () => CursorCodec.DecodeConversation("not-base64!!");
        act.Should().Throw<InvalidChatCursorException>();
    }

    [Fact]
    public void DecodeThreadActivity_Garbage_ThrowsInvalidChatCursorException()
    {
        var act = () => CursorCodec.DecodeThreadActivity("not-base64!!");
        act.Should().Throw<InvalidChatCursorException>();
    }
}
