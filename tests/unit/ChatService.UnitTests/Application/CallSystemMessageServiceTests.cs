using FluentAssertions;
using NSubstitute;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Calls;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Realtime;

namespace ChatService.UnitTests.Application;

public sealed class CallSystemMessageServiceTests
{
    private const string ConversationId = "direct-1";

    private static readonly Guid CallerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CalleeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CallId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset OccurredAt = new(2026, 5, 30, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleIncomingAsync_creates_audio_started_message()
    {
        var fixture = new Fixture();

        await fixture.Service.HandleIncomingAsync(Incoming(CallType.Audio), CancellationToken.None);

        var message = fixture.InsertedMessages.Should().ContainSingle().Subject;
        message.Kind.Should().Be(MessageKind.SystemCall);
        message.Body.Should().Be("Звонок");
        message.ClientMessageId.Should().Be($"call:{CallId:N}:started");
        message.SystemCall.Should().NotBeNull();
        message.SystemCall!.CallType.Should().Be(CallType.Audio);
        message.SystemCall.Status.Should().Be(SystemCallStatus.Started);
        message.SystemCall.CallerId.Should().Be(CallerId);
        fixture.LastPreview.Should().NotBeNull();
        fixture.LastPreview!.Body.Should().Be("Звонок");
        fixture.MessageReceivedCount.Should().Be(1);
        fixture.ConversationUpdatedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleIncomingAsync_creates_video_started_message()
    {
        var fixture = new Fixture();

        await fixture.Service.HandleIncomingAsync(Incoming(CallType.Video), CancellationToken.None);

        var message = fixture.InsertedMessages.Should().ContainSingle().Subject;
        message.Body.Should().Be("Видеозвонок");
        message.SystemCall!.CallType.Should().Be(CallType.Video);
        message.SystemCall.Status.Should().Be(SystemCallStatus.Started);
    }

    [Fact]
    public async Task HandleMissedAsync_creates_missed_message()
    {
        var fixture = new Fixture();

        await fixture.Service.HandleMissedAsync(Missed(TimeSpan.FromSeconds(45)), CancellationToken.None);

        var message = fixture.InsertedMessages.Should().ContainSingle().Subject;
        message.Body.Should().Be("Пропущенный звонок");
        message.ClientMessageId.Should().Be($"call:{CallId:N}:missed:{CalleeId:N}");
        message.SystemCall!.Status.Should().Be(SystemCallStatus.Missed);
        message.SystemCall.Duration.Should().Be(TimeSpan.FromSeconds(45));
        message.SystemCall.EndReason.Should().Be(CallEndReason.Missed);
    }

    [Fact]
    public async Task HandleEndedAsync_creates_completed_message_with_minute_duration()
    {
        var fixture = new Fixture();

        await fixture.Service.HandleEndedAsync(Ended(CallEndReason.Completed, TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(12))), CancellationToken.None);

        var message = fixture.InsertedMessages.Should().ContainSingle().Subject;
        message.Body.Should().Be("Звонок завершён • 3:12");
        message.SystemCall!.Status.Should().Be(SystemCallStatus.Completed);
        message.SystemCall.Duration.Should().Be(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(12)));
        message.SystemCall.EndReason.Should().Be(CallEndReason.Completed);
    }

    [Fact]
    public async Task HandleEndedAsync_formats_hour_scale_duration()
    {
        var fixture = new Fixture();

        await fixture.Service.HandleEndedAsync(Ended(CallEndReason.Completed, new TimeSpan(1, 2, 3)), CancellationToken.None);

        fixture.InsertedMessages.Single().Body.Should().Be("Звонок завершён • 1:02:03");
    }

    [Theory]
    [InlineData(CallEndReason.DeclinedByCallee, SystemCallStatus.Declined, "Звонок отклонён")]
    [InlineData(CallEndReason.CancelledByCaller, SystemCallStatus.Cancelled, "Звонок отменён")]
    [InlineData(CallEndReason.Failed, SystemCallStatus.Failed, "Звонок завершён")]
    public async Task HandleEndedAsync_maps_end_reasons(
        CallEndReason reason,
        SystemCallStatus expectedStatus,
        string expectedBody)
    {
        var fixture = new Fixture();

        await fixture.Service.HandleEndedAsync(Ended(reason, TimeSpan.Zero), CancellationToken.None);

        var message = fixture.InsertedMessages.Should().ContainSingle().Subject;
        message.Body.Should().Be(expectedBody);
        message.SystemCall!.Status.Should().Be(expectedStatus);
        message.SystemCall.EndReason.Should().Be(reason);
    }

    [Theory]
    [InlineData(CallEndReason.Missed)]
    [InlineData(CallEndReason.NoAnswer)]
    public async Task HandleEndedAsync_ignores_missed_or_no_answer_because_missed_event_creates_message(
        CallEndReason reason)
    {
        var fixture = new Fixture();

        await fixture.Service.HandleEndedAsync(Ended(reason, TimeSpan.Zero), CancellationToken.None);

        fixture.InsertedMessages.Should().BeEmpty();
        fixture.LastMessageUpdateCount.Should().Be(0);
        fixture.MessageReceivedCount.Should().Be(0);
        fixture.ConversationUpdatedCount.Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_client_message_id_is_deduped_without_broadcast_or_last_message_update()
    {
        var fixture = new Fixture(throwDuplicate: true);

        await fixture.Service.HandleIncomingAsync(Incoming(CallType.Audio), CancellationToken.None);

        fixture.InsertedMessages.Should().BeEmpty();
        fixture.LastMessageUpdateCount.Should().Be(0);
        fixture.MessageReceivedCount.Should().Be(0);
        fixture.ConversationUpdatedCount.Should().Be(0);
    }

    private static CallIncomingV2Event Incoming(CallType callType)
        => new(CallId, ConversationId, CallerId, [CallerId, CalleeId], callType, OccurredAt);

    private static CallMissedV2Event Missed(TimeSpan ringDuration)
        => new(CallId, ConversationId, CallerId, CalleeId, [CallerId, CalleeId], CallType.Audio, ringDuration, OccurredAt);

    private static CallEndedV2Event Ended(CallEndReason reason, TimeSpan duration)
        => new(CallId, ConversationId, CallerId, [CallerId, CalleeId], CallType.Audio, duration, reason, OccurredAt);

    private sealed class Fixture
    {
        public Fixture(bool throwDuplicate = false)
        {
            Conversations = Substitute.For<IConversationRepository>();
            Messages = Substitute.For<IMessageRepository>();
            Broadcaster = Substitute.For<IChatBroadcaster>();

            Conversation = Urfu.Link.Services.Chat.Domain.Aggregates.Conversation.Hydrate(
                ConversationId,
                ConversationType.Direct,
                [CallerId, CalleeId],
                OccurredAt,
                OccurredAt,
                lastMessagePreview: null);

            Conversations.GetByIdAsync(ConversationId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Conversation?>(Conversation));
            Conversations.UpdateLastMessageAsync(
                    Arg.Any<string>(),
                    Arg.Any<MessagePreview>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    LastMessageUpdateCount++;
                    LastPreview = callInfo.Arg<MessagePreview>();
                    return Task.CompletedTask;
                });

            if (throwDuplicate)
            {
                Messages.InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
                    .Returns(_ => throw new DuplicateClientMessageException(CallerId, $"call:{CallId:N}:started"));
            }
            else
            {
                Messages.InsertAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
                    .Returns(callInfo =>
                    {
                        InsertedMessages.Add(callInfo.Arg<Message>());
                        return Task.CompletedTask;
                    });
            }

            Broadcaster.NotifyMessageReceivedAsync(
                    Arg.Any<IReadOnlyList<Guid>>(),
                    Arg.Any<MessageDto>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    MessageReceivedCount++;
                    LastMessageDto = callInfo.Arg<MessageDto>();
                    return Task.CompletedTask;
                });
            Broadcaster.NotifyConversationUpdatedAsync(
                    Arg.Any<IReadOnlyList<Guid>>(),
                    Arg.Any<ConversationDto>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    ConversationUpdatedCount++;
                    LastConversationDto = callInfo.Arg<ConversationDto>();
                    return Task.CompletedTask;
                });

            Service = new CallSystemMessageService(Conversations, Messages, Broadcaster);
        }

        public IConversationRepository Conversations { get; }

        public IMessageRepository Messages { get; }

        public IChatBroadcaster Broadcaster { get; }

        public Conversation Conversation { get; }

        public CallSystemMessageService Service { get; }

        public List<Message> InsertedMessages { get; } = [];

        public int LastMessageUpdateCount { get; private set; }

        public int MessageReceivedCount { get; private set; }

        public int ConversationUpdatedCount { get; private set; }

        public MessagePreview? LastPreview { get; private set; }

        public MessageDto? LastMessageDto { get; private set; }

        public ConversationDto? LastConversationDto { get; private set; }
    }
}
