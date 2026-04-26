using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain.ValueObjects;

public class ReplyToTests
{
    private static readonly Guid MessageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SenderId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Create_ShortBody_KeepsBodyAsIs()
    {
        var replyTo = ReplyTo.Create(MessageId, SenderId, "hello");

        replyTo.Preview.Should().Be("hello");
        replyTo.MessageId.Should().Be(MessageId);
        replyTo.SenderId.Should().Be(SenderId);
    }

    [Fact]
    public void Create_LongBody_TruncatesToMaxPreviewLength()
    {
        var body = new string('a', ReplyTo.MaxPreviewLength + 50);

        var replyTo = ReplyTo.Create(MessageId, SenderId, body);

        replyTo.Preview.Length.Should().Be(ReplyTo.MaxPreviewLength);
    }

    [Fact]
    public void Create_BodyWithSurrogatePairAtBoundary_DoesNotSplitPair()
    {
        // Build a body where index (MaxPreviewLength - 1) lands on a high surrogate.
        var prefix = new string('a', ReplyTo.MaxPreviewLength - 1);
        var rocket = "🚀"; // 🚀 is encoded as surrogate pair (high, low)
        var body = prefix + rocket + new string('b', 50);

        var replyTo = ReplyTo.Create(MessageId, SenderId, body);

        replyTo.Preview.Length.Should().Be(ReplyTo.MaxPreviewLength - 1);
        char.IsHighSurrogate(replyTo.Preview[^1]).Should().BeFalse();
    }

    [Fact]
    public void Create_NullOrEmptyBody_ProducesEmptyPreview()
    {
        ReplyTo.Create(MessageId, SenderId, null!).Preview.Should().BeEmpty();
        ReplyTo.Create(MessageId, SenderId, string.Empty).Preview.Should().BeEmpty();
    }
}
