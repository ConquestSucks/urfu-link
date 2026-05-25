using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;

namespace NotificationService.UnitTests.Application;

public sealed class ChatMessageSentHandlerTests
{
    [Fact]
    public async Task PrepareAsync_PlainMessage_ReturnsNoDrafts()
    {
        var evt = new ChatMessageSentEvent(
            ConversationId: Guid.NewGuid().ToString(),
            MessageId: Guid.NewGuid(),
            SenderId: Guid.NewGuid(),
            Recipients: [Guid.NewGuid()],
            Preview: "Hello",
            HasAttachments: false,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        var drafts = await new ChatMessageSentHandler().PrepareAsync(evt, default);

        drafts.Should().BeEmpty();
    }

    [Fact]
    public async Task PrepareAsync_MessageWithMentions_ReturnsNoDrafts()
    {
        var mentioned = Guid.NewGuid();
        var evt = new ChatMessageSentEvent(
            ConversationId: Guid.NewGuid().ToString(),
            MessageId: Guid.NewGuid(),
            SenderId: Guid.NewGuid(),
            Recipients: [mentioned],
            Preview: "Hey @user",
            HasAttachments: false,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            Mentions: [mentioned]);

        var drafts = await new ChatMessageSentHandler().PrepareAsync(evt, default);

        drafts.Should().BeEmpty("mention notifications are produced from chat.mention.created.v1 only");
    }
}
