using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Disciplines;

/// <summary>
/// Reacts to discipline integration events by keeping the corresponding group conversation
/// in sync. All operations are idempotent: the discipline-events topic is at-least-once,
/// so each handler must tolerate replays. After every state change the corresponding SignalR
/// notification is emitted so connected clients see participant updates in real time, and
/// the matching chat integration event is published on <c>urfu.chat.events.v1</c> for any
/// downstream consumer (notifications, search, analytics).
/// </summary>
public sealed class DisciplineConversationService(
    IConversationRepository conversations,
    IChatBroadcaster broadcaster,
    ChatEventDispatcher dispatcher)
{
    public async Task HandleDisciplineCreatedAsync(
        DisciplineCreatedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var existing = await conversations
            .GetByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        var conversation = Conversation.OpenDiscipline(
            evt.DisciplineId,
            evt.OwnerTeacherId,
            evt.OccurredAtUtc,
            evt.Title,
            evt.CoverAssetId);
        var created = await conversations.TryCreateAsync(conversation, cancellationToken).ConfigureAwait(false);
        if (!created)
        {
            // A concurrent at-least-once delivery materialised the same conversation first.
            // No broadcast — that delivery already covered it.
            return;
        }

        var dto = ConversationDto.FromDomain(conversation);
        await broadcaster
            .NotifyConversationCreatedAsync([evt.OwnerTeacherId], dto, cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatDisciplineConversationCreatedEvent(
                conversation.Id,
                evt.DisciplineId,
                evt.OwnerTeacherId,
                evt.Title,
                evt.CoverAssetId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleDisciplineUpdatedAsync(
        DisciplineUpdatedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var conversation = await conversations
            .GetByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var updated = await conversations
            .UpdateMetadataAsync(conversation.Id, evt.Title, evt.CoverAssetId, cancellationToken)
            .ConfigureAwait(false);
        if (!updated)
        {
            return;
        }

        // Apply the same projection in-memory so the broadcast carries the new title/cover.
        conversation.UpdateMetadata(evt.Title, evt.CoverAssetId);
        var dto = ConversationDto.FromDomain(conversation);
        await broadcaster
            .NotifyConversationUpdatedAsync(conversation.Participants.ToList(), dto, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task HandleUserEnrolledAsync(
        UserEnrolledEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var conversation = await conversations
            .GetByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var role = MapRole(evt.Role);
        var added = await conversations
            .AddParticipantAsync(conversation.Id, evt.UserId, role, cancellationToken)
            .ConfigureAwait(false);
        if (!added)
        {
            // Already a participant — duplicate event, no broadcast.
            return;
        }

        // Notify everyone who was already in the conversation that a new participant joined —
        // computed against the pre-add roster so we don't notify the newly added user via this
        // path (they get a ConversationCreated below instead).
        var existingParticipants = conversation.Participants.ToList();
        if (existingParticipants.Count > 0)
        {
            await broadcaster
                .NotifyParticipantJoinedAsync(existingParticipants, conversation.Id, evt.UserId, role, cancellationToken)
                .ConfigureAwait(false);
        }

        // Mirror the persistence write into the in-memory aggregate so the DTO carries the
        // post-add roster without re-reading the document.
        conversation.AddParticipant(evt.UserId, role);
        var dto = ConversationDto.FromDomain(conversation);
        await broadcaster
            .NotifyConversationCreatedAsync([evt.UserId], dto, cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatParticipantJoinedEvent(conversation.Id, evt.UserId, MapToContract(role)),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleUserUnenrolledAsync(
        UserUnenrolledEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var conversation = await conversations
            .GetByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var removed = await conversations
            .RemoveParticipantAsync(conversation.Id, evt.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (!removed)
        {
            return;
        }

        // Notify the pre-removal participant list — including the unenrolled user themselves so
        // their other open sessions can drop the chat from their list.
        await broadcaster
            .NotifyParticipantLeftAsync(conversation.Participants.ToList(), conversation.Id, evt.UserId, cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatParticipantLeftEvent(conversation.Id, evt.UserId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleEnrollmentRoleChangedAsync(
        EnrollmentRoleChangedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var conversation = await conversations
            .GetByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var newRole = MapRole(evt.NewRole);
        var changed = await conversations
            .ChangeParticipantRoleAsync(conversation.Id, evt.UserId, newRole, cancellationToken)
            .ConfigureAwait(false);
        if (!changed)
        {
            return;
        }

        await broadcaster
            .NotifyParticipantRoleChangedAsync(conversation.Participants.ToList(), conversation.Id, evt.UserId, newRole, cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatParticipantRoleChangedEvent(
                conversation.Id,
                evt.UserId,
                MapToContract(MapRole(evt.OldRole)),
                MapToContract(newRole)),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleDisciplineDeletedAsync(
        DisciplineDeletedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var conversation = await conversations
            .GetByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var archived = await conversations
            .ArchiveAsync(conversation.Id, evt.OccurredAtUtc, cancellationToken)
            .ConfigureAwait(false);
        if (!archived)
        {
            return;
        }

        await broadcaster
            .NotifyConversationArchivedAsync(conversation.Participants.ToList(), conversation.Id, evt.OccurredAtUtc, cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatConversationArchivedEvent(conversation.Id, evt.OccurredAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

    private static ParticipantRole MapRole(DisciplineRole role) => role switch
    {
        DisciplineRole.Teacher => ParticipantRole.Teacher,
        DisciplineRole.Student => ParticipantRole.Student,
        _ => ParticipantRole.Member,
    };

    private static ChatParticipantRole MapToContract(ParticipantRole role) => role switch
    {
        ParticipantRole.Teacher => ChatParticipantRole.Teacher,
        ParticipantRole.Student => ChatParticipantRole.Student,
        _ => ChatParticipantRole.Member,
    };
}
