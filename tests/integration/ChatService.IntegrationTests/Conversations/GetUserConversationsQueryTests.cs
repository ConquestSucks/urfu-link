using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace ChatService.IntegrationTests.Conversations;

[Collection(IntegrationCollection.Name)]
public class GetUserConversationsQueryTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public GetUserConversationsQueryTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Execute_PagesThroughAllConversationsForParticipant_OrderedNewestFirst()
    {
        var user = Guid.NewGuid();

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var send = seedScope.ServiceProvider.GetRequiredService<SendMessageService>();
            for (var i = 0; i < 5; i++)
            {
                var conv = await open.OpenAsync(user, Guid.NewGuid(), default);
                await send.SendAsync(
                    new SendMessageRequest(
                        conv.Id,
                        user,
                        $"seed-{i}",
                        Array.Empty<Guid>(),
                        $"c-{Guid.NewGuid():N}",
                        PeerUserId: conv.Participants.Single(p => p != user)),
                    default);
                await Task.Delay(5);
            }
        }

        await using var queryScope = _factory.Services.CreateAsyncScope();
        var query = queryScope.ServiceProvider.GetRequiredService<GetUserConversationsQuery>();

        var firstPage = await query.ExecuteAsync(user, cursor: null, limit: 3, ConversationListFilter.All, default);
        firstPage.Items.Should().HaveCount(3);
        firstPage.NextCursor.Should().NotBeNullOrEmpty();

        var secondPage = await query.ExecuteAsync(user, cursor: firstPage.NextCursor, limit: 3, ConversationListFilter.All, default);
        secondPage.Items.Should().HaveCount(2);
        secondPage.NextCursor.Should().BeNull();

        // No overlap
        var allIds = firstPage.Items.Concat(secondPage.Items).Select(c => c.Id).ToList();
        allIds.Distinct().Should().HaveCount(allIds.Count);
    }

    [Fact]
    public async Task Execute_DoesNotIncludeOtherUsersConversations()
    {
        var user = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var send = seedScope.ServiceProvider.GetRequiredService<SendMessageService>();
            var mine = await open.OpenAsync(user, Guid.NewGuid(), default);
            var theirs = await open.OpenAsync(stranger, Guid.NewGuid(), default);
            await send.SendAsync(
                new SendMessageRequest(
                    mine.Id,
                    user,
                    "mine",
                    Array.Empty<Guid>(),
                    $"c-{Guid.NewGuid():N}",
                    PeerUserId: mine.Participants.Single(p => p != user)),
                default);
            await send.SendAsync(
                new SendMessageRequest(
                    theirs.Id,
                    stranger,
                    "theirs",
                    Array.Empty<Guid>(),
                    $"c-{Guid.NewGuid():N}",
                    PeerUserId: theirs.Participants.Single(p => p != stranger)),
                default);
        }

        await using var queryScope = _factory.Services.CreateAsyncScope();
        var query = queryScope.ServiceProvider.GetRequiredService<GetUserConversationsQuery>();
        var page = await query.ExecuteAsync(user, cursor: null, limit: 50, ConversationListFilter.All, default);

        page.Items.Should().HaveCount(1);
        page.Items[0].Participants.Should().Contain(user);
    }

    [Fact]
    public async Task Execute_EnrichesOwnLatestPreviewWithReadState()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        string conversationId;
        Guid messageId;

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var send = seedScope.ServiceProvider.GetRequiredService<SendMessageService>();
            var markRead = seedScope.ServiceProvider.GetRequiredService<MarkReadService>();

            var conversation = await open.OpenAsync(sender, peer, default);
            conversationId = conversation.Id;

            var message = await send.SendAsync(
                new SendMessageRequest(
                    conversation.Id,
                    sender,
                    "already read",
                    Array.Empty<Guid>(),
                    $"c-{Guid.NewGuid():N}",
                    PeerUserId: peer),
                default);
            messageId = message.Id;

            await markRead.MarkAsync(new MarkReadRequest(conversation.Id, peer, message.Id), default);
        }

        await using var queryScope = _factory.Services.CreateAsyncScope();
        var query = queryScope.ServiceProvider.GetRequiredService<GetUserConversationsQuery>();
        var page = await query.ExecuteAsync(sender, cursor: null, limit: 50, ConversationListFilter.All, default);

        var listedConversation = page.Items.Should().ContainSingle(c => c.Id == conversationId).Subject;
        listedConversation.LastMessagePreview.Should().NotBeNull();
        var preview = listedConversation.LastMessagePreview!;
        preview.MessageId.Should().Be(messageId);
        preview.ReadAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_IncludesUnreadCountForIncomingMessagesBeforeHistoryIsLoaded()
    {
        var user = Guid.NewGuid();
        var peer = Guid.NewGuid();
        string conversationId;
        Guid latestMessageId = Guid.Empty;

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var send = seedScope.ServiceProvider.GetRequiredService<SendMessageService>();
            var markRead = seedScope.ServiceProvider.GetRequiredService<MarkReadService>();

            var conversation = await open.OpenAsync(user, peer, default);
            conversationId = conversation.Id;

            var alreadyRead = await send.SendAsync(
                new SendMessageRequest(
                    conversation.Id,
                    peer,
                    "already read",
                    Array.Empty<Guid>(),
                    $"c-{Guid.NewGuid():N}",
                    PeerUserId: user),
                default);
            await markRead.MarkAsync(new MarkReadRequest(conversation.Id, user, alreadyRead.Id), default);

            var ownMessage = await send.SendAsync(
                new SendMessageRequest(
                    conversation.Id,
                    user,
                    "own latest should not count",
                    Array.Empty<Guid>(),
                    $"c-{Guid.NewGuid():N}",
                    PeerUserId: peer),
                default);
            await markRead.MarkAsync(new MarkReadRequest(conversation.Id, peer, ownMessage.Id), default);

            for (var i = 0; i < 3; i++)
            {
                var sent = await send.SendAsync(
                    new SendMessageRequest(
                        conversation.Id,
                        peer,
                        $"offline-{i}",
                        Array.Empty<Guid>(),
                        $"c-{Guid.NewGuid():N}",
                        PeerUserId: user),
                    default);
                latestMessageId = sent.Id;
            }
        }

        await using var queryScope = _factory.Services.CreateAsyncScope();
        var query = queryScope.ServiceProvider.GetRequiredService<GetUserConversationsQuery>();
        var userPage = await query.ExecuteAsync(user, cursor: null, limit: 50, ConversationListFilter.All, default);
        var peerPage = await query.ExecuteAsync(peer, cursor: null, limit: 50, ConversationListFilter.All, default);

        var userConversation = userPage.Items.Should().ContainSingle(c => c.Id == conversationId).Subject;
        userConversation.LastMessagePreview.Should().NotBeNull();
        userConversation.LastMessagePreview!.MessageId.Should().Be(latestMessageId);
        userConversation.UnreadCount.Should().Be(3);

        peerPage.Items.Should().ContainSingle(c => c.Id == conversationId)
            .Subject.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task Execute_IncludesAttachmentFileNamesForFileOnlyPreview()
    {
        var sender = Guid.NewGuid();
        var peer = Guid.NewGuid();
        var firstAssetId = Guid.NewGuid();
        var secondAssetId = Guid.NewGuid();
        string conversationId;

        _factory.MediaServiceClient.RegisterAsset(
            firstAssetId,
            ownerId: sender,
            kind: AttachmentType.Document,
            mimeType: "application/pdf",
            fileName: "report.pdf");
        _factory.MediaServiceClient.RegisterAsset(
            secondAssetId,
            ownerId: sender,
            kind: AttachmentType.Document,
            mimeType: "application/json",
            fileName: "data.json");

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var send = seedScope.ServiceProvider.GetRequiredService<SendMessageService>();

            var conversation = await open.OpenAsync(sender, peer, default);
            conversationId = conversation.Id;

            await send.SendAsync(
                new SendMessageRequest(
                    conversation.Id,
                    sender,
                    string.Empty,
                    new[] { firstAssetId, secondAssetId },
                    $"c-{Guid.NewGuid():N}",
                    PeerUserId: peer),
                default);
        }

        await using var queryScope = _factory.Services.CreateAsyncScope();
        var query = queryScope.ServiceProvider.GetRequiredService<GetUserConversationsQuery>();
        var page = await query.ExecuteAsync(sender, cursor: null, limit: 50, ConversationListFilter.All, default);

        var listedConversation = page.Items.Should().ContainSingle(c => c.Id == conversationId).Subject;
        listedConversation.LastMessagePreview.Should().NotBeNull();
        listedConversation.LastMessagePreview!.Body.Should().BeEmpty();
        listedConversation.LastMessagePreview.HasAttachments.Should().BeTrue();
        listedConversation.LastMessagePreview.AttachmentFileNames
            .Should().Equal("report.pdf", "data.json");
    }
}
