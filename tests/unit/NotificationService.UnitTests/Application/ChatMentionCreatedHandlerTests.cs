using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Application;

public sealed class ChatMentionCreatedHandlerTests
{
    [Fact]
    public async Task PrepareAsync_UsesMessageSourceActionAndMentionPriority()
    {
        var sender = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var evt = new ChatMentionCreatedEvent(
            conversationId.ToString(),
            messageId,
            sender,
            [recipient],
            DateTimeOffset.UtcNow);

        var intents = await new ChatMentionCreatedHandler().PrepareAsync(evt, default);

        var intent = intents.Single();
        intent.RecipientUserId.Should().Be(recipient);
        intent.Category.Should().Be(NotificationCategory.ChatMessageMention);
        intent.Priority.Should().Be(NotificationPriority.Mention);
        intent.SourceActionId.Should().Be($"chat:message:{conversationId}:{messageId:N}");
        intent.SuppressWhenViewingContextKey.Should().Be($"chat:conversation:{conversationId}");
    }

    [Fact]
    public async Task PrepareAsync_SkipsSelfMention()
    {
        var sender = Guid.NewGuid();
        var evt = new ChatMentionCreatedEvent(
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            sender,
            [sender],
            DateTimeOffset.UtcNow);

        var intents = await new ChatMentionCreatedHandler().PrepareAsync(evt, default);

        intents.Should().BeEmpty();
    }
}
