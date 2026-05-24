using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record RemoveReactionRequest(Guid MessageId, Guid UserId, string Emoji);

public sealed class RemoveReactionService(
    IConversationRepository conversations,
    IMessageRepository messages,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task RemoveAsync(RemoveReactionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Emoji);

        var message = await messages.GetByIdAsync(request.MessageId, cancellationToken).ConfigureAwait(false)
            ?? throw ChatMessageNotFoundException.For(request.MessageId);

        var conversation = await conversations.GetByIdAsync(message.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(message.ConversationId);

        if (!conversation.IsParticipant(request.UserId))
        {
            throw new ChatAccessDeniedException(message.ConversationId, request.UserId);
        }

        var removed = await messages.RemoveReactionAsync(
            request.MessageId, request.UserId, request.Emoji, cancellationToken).ConfigureAwait(false);

        if (!removed)
        {
            return;
        }

        var now = clock.GetUtcNow();
        await dispatcher.PublishAsync(
            new ChatReactionRemovedEvent(conversation.Id, request.MessageId, request.UserId, request.Emoji, now),
            cancellationToken).ConfigureAwait(false);

        var summary = await BuildSummaryAsync(request.MessageId, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyReactionUpdatedAsync(
            conversation.Participants.ToList(), request.MessageId, summary, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> BuildSummaryAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var refreshed = await messages.GetByIdAsync(messageId, cancellationToken).ConfigureAwait(false);
        if (refreshed is null)
        {
            return new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.Ordinal);
        }

        return refreshed.Reactions
            .GroupBy(r => r.Emoji, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(r => r.UserId).ToList(),
                StringComparer.Ordinal);
    }
}
