using FluentAssertions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class SendMessageServiceTests
{
    private static readonly Guid Sender = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Peer = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IMediaServiceClient _media = Substitute.For<IMediaServiceClient>();
    private readonly IIdempotencyStore _idempotency = Substitute.For<IIdempotencyStore>();
    private readonly IChatBroadcaster _broadcaster = Substitute.For<IChatBroadcaster>();
    private readonly RecordingOutboxWriter _outbox = new();

    private SendMessageService Build()
    {
        var dispatcher = new ChatEventDispatcher(
            _outbox,
            new ServiceProfile("chat-service", "mongodb", KafkaTopicNames.ChatEvents, "chat.message.sent.v1"));
        var options = Microsoft.Extensions.Options.Options.Create(new Urfu.Link.Services.Chat.Infrastructure.ChatOptions());
        return new SendMessageService(_conversations, _messages, _media, _idempotency, dispatcher, _broadcaster, TimeProvider.System, options);
    }

    private Conversation SeedConversation()
    {
        var conv = Conversation.OpenDirect(Sender, Peer, DateTimeOffset.UtcNow);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaAssetMetadata>());
        return conv;
    }

    [Fact]
    public async Task SendAsync_NonParticipant_ThrowsAccessDenied()
    {
        var conv = SeedConversation();
        var stranger = Guid.NewGuid();
        var request = new SendMessageRequest(conv.Id, stranger, "x", Array.Empty<Guid>(), "c1");

        var act = () => Build().SendAsync(request, default);

        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    [Fact]
    public async Task SendAsync_InArchivedConversation_ThrowsArchived()
    {
        var teacherId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var conv = Conversation.OpenDiscipline(disciplineId, teacherId, DateTimeOffset.UtcNow);
        conv.Archive(DateTimeOffset.UtcNow);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaAssetMetadata>());

        var request = new SendMessageRequest(conv.Id, teacherId, "x", Array.Empty<Guid>(), "c1");

        await Build().Invoking(s => s.SendAsync(request, default))
            .Should().ThrowAsync<ChatConversationArchivedException>();
    }

    [Fact]
    public async Task SendAsync_InAnnouncementOnly_AsStudent_ThrowsAnnouncementOnly()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var conv = Conversation.OpenDiscipline(disciplineId, teacherId, DateTimeOffset.UtcNow);
        conv.AddParticipant(studentId, ParticipantRole.Student);
        conv.SetAnnouncementOnly(true);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaAssetMetadata>());

        var request = new SendMessageRequest(conv.Id, studentId, "x", Array.Empty<Guid>(), "c1");

        await Build().Invoking(s => s.SendAsync(request, default))
            .Should().ThrowAsync<ChatAnnouncementOnlyException>();
    }

    [Fact]
    public async Task SendAsync_InAnnouncementOnly_AsTeacher_Succeeds()
    {
        var teacherId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var conv = Conversation.OpenDiscipline(disciplineId, teacherId, DateTimeOffset.UtcNow);
        conv.SetAnnouncementOnly(true);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaAssetMetadata>());

        var dto = await Build().SendAsync(
            new SendMessageRequest(conv.Id, teacherId, "x", Array.Empty<Guid>(), "c1"),
            default);

        dto.AuthorRole.Should().Be(ParticipantRole.Teacher);
    }

    [Fact]
    public async Task SendAsync_InAnnouncementOnly_AsAdmin_Succeeds()
    {
        var teacherId = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var conv = Conversation.OpenDiscipline(disciplineId, teacherId, DateTimeOffset.UtcNow);
        conv.SetAnnouncementOnly(true);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaAssetMetadata>());

        var dto = await Build().SendAsync(
            new SendMessageRequest(conv.Id, admin, "x", Array.Empty<Guid>(), "c1", CallerIsAdmin: true),
            default);

        dto.SenderId.Should().Be(admin);
    }

    [Fact]
    public async Task SendAsync_StoresAuthorRoleFromConversation_Student()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var conv = Conversation.OpenDiscipline(disciplineId, teacherId, DateTimeOffset.UtcNow);
        conv.AddParticipant(studentId, ParticipantRole.Student);
        _conversations.GetByIdAsync(conv.Id, Arg.Any<CancellationToken>()).Returns(conv);
        _idempotency.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaAssetMetadata>());

        var dto = await Build().SendAsync(
            new SendMessageRequest(conv.Id, studentId, "x", Array.Empty<Guid>(), "c1"),
            default);

        dto.AuthorRole.Should().Be(ParticipantRole.Student);
    }

    [Fact]
    public async Task SendAsync_ConversationMissing_ThrowsNotFound()
    {
        _conversations.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Conversation?)null);

        var act = () => Build().SendAsync(
            new SendMessageRequest("missing", Sender, "x", Array.Empty<Guid>(), "c1"),
            default);

        await act.Should().ThrowAsync<ConversationNotFoundException>();
    }

    [Fact]
    public async Task SendAsync_HappyPath_PersistsAndPublishesSentEvent()
    {
        var conv = SeedConversation();
        var request = new SendMessageRequest(conv.Id, Sender, "hello", Array.Empty<Guid>(), "c1");

        var dto = await Build().SendAsync(request, default);

        dto.Body.Should().Be("hello");
        dto.SenderId.Should().Be(Sender);

        await _messages.Received(1).InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
        await _conversations.Received(1).UpdateLastMessageAsync(
            conv.Id, Arg.Any<MessagePreview>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        _outbox.Captured.OfType<ChatMessageSentEvent>().Should().ContainSingle()
            .Which.Should().Match<ChatMessageSentEvent>(e =>
                e.SenderId == Sender && e.Recipients.Count == 1 && e.Recipients[0] == Peer);
    }

    [Fact]
    public async Task SendAsync_DuplicateClientMessageId_ReturnsPriorMessage_WithoutDoublePublish()
    {
        var conv = SeedConversation();
        var prior = Message.Send(Guid.NewGuid(), conv.Id, Sender, "old", Array.Empty<Attachment>(), "c-dup", DateTimeOffset.UtcNow);

        _idempotency.TryRegisterAsync(Arg.Is<string>(k => k.EndsWith(":c-dup", StringComparison.Ordinal)), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(false));
        _messages.FindByClientMessageIdAsync(Sender, "c-dup", Arg.Any<CancellationToken>()).Returns(prior);

        var dto = await Build().SendAsync(
            new SendMessageRequest(conv.Id, Sender, "new", Array.Empty<Guid>(), "c-dup"),
            default);

        dto.Id.Should().Be(prior.Id);
        await _messages.DidNotReceive().InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
        _outbox.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_AssetNotOwned_ThrowsAttachmentException_BeforeInsert()
    {
        var conv = SeedConversation();
        var assetId = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        _media.BatchGetMetadataAsync(
                Arg.Is<IReadOnlyList<Guid>>(l => l.Contains(assetId)),
                Arg.Any<CancellationToken>())
            .Returns(new List<MediaAssetMetadata>
            {
                new(assetId, someoneElse, AttachmentType.Image, 100, "image/png", "p.png", IsUploaded: true),
            });

        var request = new SendMessageRequest(conv.Id, Sender, "x", new[] { assetId }, "c1");

        var act = () => Build().SendAsync(request, default);

        await act.Should().ThrowAsync<ChatAttachmentNotOwnedException>();
        await _messages.DidNotReceive().InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_AssetMissingFromMediaService_ThrowsAttachmentException()
    {
        var conv = SeedConversation();
        var assetId = Guid.NewGuid();
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MediaAssetMetadata>());

        var request = new SendMessageRequest(conv.Id, Sender, "x", new[] { assetId }, "c1");

        await Build().Invoking(s => s.SendAsync(request, default))
            .Should().ThrowAsync<ChatAttachmentNotOwnedException>();
    }

    [Fact]
    public async Task SendAsync_AssetNotUploadedYet_Throws()
    {
        var conv = SeedConversation();
        var assetId = Guid.NewGuid();
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MediaAssetMetadata>
            {
                new(assetId, Sender, AttachmentType.Image, 100, "image/png", "p.png", IsUploaded: false),
            });

        var request = new SendMessageRequest(conv.Id, Sender, "x", new[] { assetId }, "c1");

        await Build().Invoking(s => s.SendAsync(request, default))
            .Should().ThrowAsync<ChatAttachmentNotOwnedException>();
    }

    [Fact]
    public async Task SendAsync_WithAttachment_PersistsServerSideMetadata_AndGrantsAccess()
    {
        var conv = SeedConversation();
        var assetId = Guid.NewGuid();
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MediaAssetMetadata>
            {
                new(assetId, Sender, AttachmentType.Image, 1024, "image/png", "photo.png", IsUploaded: true),
            });

        var request = new SendMessageRequest(conv.Id, Sender, "look", new[] { assetId }, "c1");

        var dto = await Build().SendAsync(request, default);

        dto.Attachments.Should().ContainSingle()
            .Which.Should().Match<Urfu.Link.Services.Chat.Application.Contracts.AttachmentDto>(a =>
                a.MediaAssetId == assetId
                && a.Type == AttachmentType.Image
                && a.Size == 1024
                && a.MimeType == "image/png"
                && a.FileName == "photo.png");

        await _media.Received(1).GrantConversationAccessAsync(
            assetId,
            Arg.Is<IReadOnlyList<Guid>>(u => u.Count == 1 && u[0] == Peer),
            conv.Id,
            Sender,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_BodyTooLong_ThrowsPayloadTooLarge()
    {
        var conv = SeedConversation();
        var hugeBody = new string('a', SendMessageService.MaxBodyLength + 1);
        var request = new SendMessageRequest(conv.Id, Sender, hugeBody, Array.Empty<Guid>(), "c1");

        await Build().Invoking(s => s.SendAsync(request, default))
            .Should().ThrowAsync<ChatPayloadTooLargeException>();
    }

    [Fact]
    public async Task SendAsync_TooManyAttachments_ThrowsPayloadTooLarge()
    {
        var conv = SeedConversation();
        var ids = Enumerable.Range(0, SendMessageService.MaxAttachmentsPerMessage + 1).Select(_ => Guid.NewGuid()).ToList();
        var request = new SendMessageRequest(conv.Id, Sender, "x", ids, "c1");

        await Build().Invoking(s => s.SendAsync(request, default))
            .Should().ThrowAsync<ChatPayloadTooLargeException>();
    }

    [Fact]
    public async Task SendAsync_WithMultipleAttachments_GrantsAccessInParallel()
    {
        var conv = SeedConversation();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _media.BatchGetMetadataAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ids.Select(id => new MediaAssetMetadata(id, Sender, AttachmentType.Image, 100, "image/png", "p.png", true)).ToList());

        await Build().SendAsync(new SendMessageRequest(conv.Id, Sender, "x", ids, "c1"), default);

        foreach (var id in ids)
        {
            await _media.Received(1).GrantConversationAccessAsync(
                id, Arg.Any<IReadOnlyList<Guid>>(), conv.Id, Sender, Arg.Any<CancellationToken>());
        }
    }

    private sealed class RecordingOutboxWriter : IOutboxWriter
    {
        public List<IIntegrationEvent> Captured { get; } = new();

        public ValueTask EnqueueAsync<TEvent>(string topic, IntegrationEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Captured.Add(envelope.Payload);
            return ValueTask.CompletedTask;
        }
    }
}
