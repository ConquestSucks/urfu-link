using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class EditMessageServiceTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public EditMessageServiceTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EditAsync_HappyPath_PersistsBody_AppendsHistory_AndPublishesEvent()
    {
        var (sender, peer, conv, msg) = await SeedSentMessageAsync("hello");

        await using var scope = _factory.Services.CreateAsyncScope();
        var edit = scope.ServiceProvider.GetRequiredService<EditMessageService>();

        var dto = await edit.EditAsync(new EditMessageRequest(msg.Id, sender, "edited"), default);

        dto.Body.Should().Be("edited");
        dto.EditedAtUtc.Should().NotBeNull();

        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.Body.Should().Be("edited");
        loaded.EditHistory.Should().ContainSingle();
        loaded.EditHistory[0].Body.Should().Be("hello");

        var edited = _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatMessageEditedEvent>()
            .Single(e => e.MessageId == msg.Id);
        edited.NewBody.Should().Be("edited");
        edited.EditorUserId.Should().Be(sender);

        // peer membership is implicit — we just confirm conversation context is correct.
        edited.ConversationId.Should().Be(conv.Id);
        peer.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EditAsync_EmptyBody_Throws_AndDoesNotPersist()
    {
        var (sender, _, _, msg) = await SeedSentMessageAsync("hello");

        await using var scope = _factory.Services.CreateAsyncScope();
        var edit = scope.ServiceProvider.GetRequiredService<EditMessageService>();

        var act = () => edit.EditAsync(new EditMessageRequest(msg.Id, sender, string.Empty), default);

        await act.Should().ThrowAsync<ArgumentException>();
        var loaded = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(msg.Id, default);
        loaded!.Body.Should().Be("hello");
    }

    [Fact]
    public async Task EditAsync_BodyTooLong_Throws()
    {
        var (sender, _, _, msg) = await SeedSentMessageAsync("hello");
        var oversized = new string('x', ChatBodyConstraints.MaxBodyLength + 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var edit = scope.ServiceProvider.GetRequiredService<EditMessageService>();

        var act = () => edit.EditAsync(new EditMessageRequest(msg.Id, sender, oversized), default);

        await act.Should().ThrowAsync<ChatPayloadTooLargeException>();
    }

    [Fact]
    public async Task EditAsync_NotAuthor_ThrowsNotMessageAuthor()
    {
        var (_, peer, _, msg) = await SeedSentMessageAsync("hello");

        await using var scope = _factory.Services.CreateAsyncScope();
        var edit = scope.ServiceProvider.GetRequiredService<EditMessageService>();

        var act = () => edit.EditAsync(new EditMessageRequest(msg.Id, peer, "evil"), default);

        await act.Should().ThrowAsync<ChatNotMessageAuthorException>();
    }

    [Fact]
    public async Task EditAsync_AddingMention_PublishesChatMentionCreatedForDiff()
    {
        var (sender, peer, conv, msg) = await SeedSentMessageAsync("hi");
        _factory.OutboxWriter.Clear();

        await using var scope = _factory.Services.CreateAsyncScope();
        var edit = scope.ServiceProvider.GetRequiredService<EditMessageService>();

        var newBody = $"hi @{peer:D}";
        await edit.EditAsync(new EditMessageRequest(msg.Id, sender, newBody), default);

        var mention = _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatMentionCreatedEvent>()
            .Single(e => e.MessageId == msg.Id);
        mention.MentionedUserIds.Should().Equal(peer);
        mention.ConversationId.Should().Be(conv.Id);
    }

    private async Task<(Guid sender, Guid peer, Conversation conv, Message msg)> SeedSentMessageAsync(string body)
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();

        var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
        var conv = await open.OpenAsync(sender, peer, default);

        var send = scope.ServiceProvider.GetRequiredService<SendMessageService>();
        var dto = await send.SendAsync(
            new SendMessageRequest(conv.Id, sender, body, Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
            default);

        var msg = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
            .GetByIdAsync(dto.Id, default);
        return (sender, peer, conv, msg!);
    }
}
