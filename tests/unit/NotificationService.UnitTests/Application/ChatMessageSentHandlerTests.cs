using FluentAssertions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace NotificationService.UnitTests.Application;

public sealed class ChatMessageSentHandlerTests
{
    private static IDisciplineConversationLookup NoDisciplineLookup()
    {
        var lookup = Substitute.For<IDisciplineConversationLookup>();
        lookup.IsDisciplineConversationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        return lookup;
    }

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

        var drafts = await new ChatMessageSentHandler(NoDisciplineLookup()).PrepareAsync(evt, default);

        drafts.Should().HaveCount(2);
        drafts.Select(d => d.RecipientUserId).Should().BeEquivalentTo([alice, bob]);
    }

    [Fact]
    public async Task PrepareAsync_MentionedRecipients_AreSuppressedFromGenericMessage()
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

        var drafts = await new ChatMessageSentHandler(NoDisciplineLookup()).PrepareAsync(evt, default);

        var directDraft = drafts.Single();

        directDraft.RecipientUserId.Should().Be(bob);
        directDraft.Category.Should().Be(NotificationCategory.ChatMessageDirect);
        directDraft.Severity.Should().Be(NotificationSeverity.Normal);
        drafts.Should().NotContain(d => d.RecipientUserId == alice);
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

        var drafts = await new ChatMessageSentHandler(NoDisciplineLookup()).PrepareAsync(evt, default);

        var draft = drafts.Single();
        draft.Content.DeepLink.Should().Contain(convId.ToString());
        draft.Content.DeepLink.Should().Contain(msgId.ToString("N"));
        draft.Data.Values["conversationId"].Should().Be(convId.ToString());
        draft.Data.Values["messageId"].Should().Be(msgId.ToString("N"));
        draft.Data.Values["senderId"].Should().Be(sender.ToString("N"));
        draft.Actor.Should().NotBeNull();
        draft.Actor!.Id.Should().Be(sender);
        draft.SourceActionId.Should().Be($"chat:message:{convId}:{msgId:N}");
        draft.Priority.Should().Be(NotificationPriority.ChatMessage);
        draft.SuppressWhenViewingContextKey.Should().Be($"chat:conversation:{convId}");
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

        var drafts = await new ChatMessageSentHandler(NoDisciplineLookup()).PrepareAsync(evt, default);

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

        var firstRun = await new ChatMessageSentHandler(NoDisciplineLookup()).PrepareAsync(evt, default);
        var secondRun = await new ChatMessageSentHandler(NoDisciplineLookup()).PrepareAsync(evt, default);

        firstRun.Single().GroupKey.Should().Be(secondRun.Single().GroupKey);
    }
}
