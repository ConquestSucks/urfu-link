using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Application;

public sealed class ChatMessageSentHandlerTests
{
    [Fact]
    public async Task PrepareAsync_SkipsSenderInRecipients()
    {
        var sender = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var evt = new ChatMessageSentEvent(
            ConversationId: Guid.NewGuid().ToString(),
            MessageId: Guid.NewGuid(),
            SenderId: sender,
            Recipients: [sender, alice, bob],
            Preview: "Hello",
            HasAttachments: false,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        var drafts = await new ChatMessageSentHandler().PrepareAsync(evt, default);

        drafts.Should().HaveCount(2);
        drafts.Select(d => d.RecipientUserId).Should().BeEquivalentTo([alice, bob]);
    }

    [Fact]
    public async Task PrepareAsync_MentionedRecipients_GetMentionCategoryAndHighSeverity()
    {
        var sender = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var evt = new ChatMessageSentEvent(
            ConversationId: Guid.NewGuid().ToString(),
            MessageId: Guid.NewGuid(),
            SenderId: sender,
            Recipients: [alice, bob],
            Preview: "Hey @alice",
            HasAttachments: false,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            Mentions: [alice]);

        var drafts = await new ChatMessageSentHandler().PrepareAsync(evt, default);

        var mentionDraft = drafts.Single(d => d.RecipientUserId == alice);
        var directDraft = drafts.Single(d => d.RecipientUserId == bob);

        mentionDraft.Category.Should().Be(NotificationCategory.ChatMessageMention);
        mentionDraft.Severity.Should().Be(NotificationSeverity.High);
        directDraft.Category.Should().Be(NotificationCategory.ChatMessageDirect);
        directDraft.Severity.Should().Be(NotificationSeverity.Normal);
    }

    [Fact]
    public async Task PrepareAsync_PopulatesDeepLinkAndDataMap()
    {
        var sender = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var msgId = Guid.NewGuid();
        var evt = new ChatMessageSentEvent(
            ConversationId: convId.ToString(),
            MessageId: msgId,
            SenderId: sender,
            Recipients: [bob],
            Preview: "Hello",
            HasAttachments: false,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        var drafts = await new ChatMessageSentHandler().PrepareAsync(evt, default);

        var draft = drafts.Single();
        draft.Content.DeepLink.Should().Contain(convId.ToString());
        draft.Content.DeepLink.Should().Contain(msgId.ToString("N"));
        draft.Data.Values["conversationId"].Should().Be(convId.ToString());
        draft.Data.Values["messageId"].Should().Be(msgId.ToString("N"));
        draft.Data.Values["senderId"].Should().Be(sender.ToString("N"));
    }

    [Fact]
    public async Task PrepareAsync_PreviewFallbackForBlankBody()
    {
        var sender = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var evt = new ChatMessageSentEvent(
            ConversationId: Guid.NewGuid().ToString(),
            MessageId: Guid.NewGuid(),
            SenderId: sender,
            Recipients: [bob],
            Preview: "",
            HasAttachments: true,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        var drafts = await new ChatMessageSentHandler().PrepareAsync(evt, default);

        drafts.Single().Content.Body.Should().Be("Новое сообщение");
    }

    [Fact]
    public async Task PrepareAsync_NonGuidConversationId_ProducesStableGroupKey()
    {
        var sender = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var evt = new ChatMessageSentEvent(
            ConversationId: "stringy-conversation-id",
            MessageId: Guid.NewGuid(),
            SenderId: sender,
            Recipients: [bob],
            Preview: "Hi",
            HasAttachments: false,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        var firstRun = await new ChatMessageSentHandler().PrepareAsync(evt, default);
        var secondRun = await new ChatMessageSentHandler().PrepareAsync(evt, default);

        firstRun.Single().GroupKey.Should().Be(secondRun.Single().GroupKey);
    }
}
