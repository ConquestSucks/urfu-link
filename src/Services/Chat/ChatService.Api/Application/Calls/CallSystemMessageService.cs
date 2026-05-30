using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Calls;

public sealed class CallSystemMessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IChatBroadcaster broadcaster)
{
    public Task HandleIncomingAsync(CallIncomingV2Event evt, CancellationToken cancellationToken)
    {
        var body = evt.CallType == CallType.Video ? "Видеозвонок" : "Звонок";
        return CreateSystemMessageAsync(
            evt.ConversationId,
            evt.CallerId,
            body,
            $"call:{evt.CallId:N}:started",
            evt.OccurredAtUtc,
            evt.CallId,
            evt.CallType,
            SystemCallStatus.Started,
            evt.CallerId,
            duration: null,
            endReason: null,
            cancellationToken);
    }

    public Task HandleMissedAsync(CallMissedV2Event evt, CancellationToken cancellationToken)
        => CreateSystemMessageAsync(
            evt.ConversationId,
            evt.CallerId,
            "Пропущенный звонок",
            $"call:{evt.CallId:N}:missed:{evt.RecipientId:N}",
            evt.OccurredAtUtc,
            evt.CallId,
            evt.CallType,
            SystemCallStatus.Missed,
            evt.CallerId,
            evt.RingDuration,
            CallEndReason.Missed,
            cancellationToken);

    public Task HandleEndedAsync(CallEndedV2Event evt, CancellationToken cancellationToken)
    {
        if (evt.Reason is CallEndReason.Missed or CallEndReason.NoAnswer)
        {
            return Task.CompletedTask;
        }

        var status = evt.Reason switch
        {
            CallEndReason.Completed => SystemCallStatus.Completed,
            CallEndReason.DeclinedByCallee => SystemCallStatus.Declined,
            CallEndReason.CancelledByCaller => SystemCallStatus.Cancelled,
            _ => SystemCallStatus.Failed,
        };

        var body = evt.Reason switch
        {
            CallEndReason.Completed => $"Звонок завершён • {FormatDuration(evt.Duration)}",
            CallEndReason.DeclinedByCallee => "Звонок отклонён",
            CallEndReason.CancelledByCaller => "Звонок отменён",
            _ => "Звонок завершён",
        };

        return CreateSystemMessageAsync(
            evt.ConversationId,
            evt.CallerId,
            body,
            $"call:{evt.CallId:N}:ended:{evt.Reason}",
            evt.OccurredAtUtc,
            evt.CallId,
            evt.CallType,
            status,
            evt.CallerId,
            evt.Duration,
            evt.Reason,
            cancellationToken);
    }

    private async Task CreateSystemMessageAsync(
        string conversationId,
        Guid senderId,
        string body,
        string clientMessageId,
        DateTimeOffset occurredAtUtc,
        Guid callId,
        CallType callType,
        SystemCallStatus status,
        Guid callerId,
        TimeSpan? duration,
        CallEndReason? endReason,
        CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var message = Message.SystemCall(
            Guid.NewGuid(),
            conversationId,
            senderId,
            body,
            clientMessageId,
            occurredAtUtc,
            callId,
            callType,
            status,
            callerId,
            duration,
            endReason);

        try
        {
            await messages.InsertAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateClientMessageException)
        {
            return;
        }

        var preview = new MessagePreview(senderId, body, occurredAtUtc, hasAttachments: false);
        await conversations.UpdateLastMessageAsync(conversationId, preview, occurredAtUtc, cancellationToken).ConfigureAwait(false);

        var dto = MessageDto.FromDomain(message);
        await broadcaster.NotifyMessageReceivedAsync(conversation.Participants, dto, cancellationToken).ConfigureAwait(false);

        var updated = await conversations.GetByIdAsync(conversationId, cancellationToken).ConfigureAwait(false);
        if (updated is not null)
        {
            await broadcaster.NotifyConversationUpdatedAsync(
                updated.Participants,
                ConversationDto.FromDomain(updated),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
}
