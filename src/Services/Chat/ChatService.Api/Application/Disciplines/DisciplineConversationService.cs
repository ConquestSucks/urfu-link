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
/// notification is emitted so connected clients see participant updates in real time.
/// </summary>
public sealed class DisciplineConversationService(
    IConversationRepository conversations,
    IChatBroadcaster broadcaster)
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

        // Notify everyone who was already in the conversation that a new participant joined.
        var existingParticipants = conversation.Participants.Where(p => p != evt.UserId).ToList();
        if (existingParticipants.Count > 0)
        {
            await broadcaster
                .NotifyParticipantJoinedAsync(existingParticipants, conversation.Id, evt.UserId, role, cancellationToken)
                .ConfigureAwait(false);
        }

        // The newly enrolled user gets a ConversationCreated so the chat appears in their list
        // without a manual refresh. Re-load the document so the DTO reflects the post-add roles.
        var refreshed = await conversations
            .GetByIdAsync(conversation.Id, cancellationToken)
            .ConfigureAwait(false);
        var dto = ConversationDto.FromDomain(refreshed ?? conversation);
        await broadcaster
            .NotifyConversationCreatedAsync([evt.UserId], dto, cancellationToken)
            .ConfigureAwait(false);
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
    }

    private static ParticipantRole MapRole(DisciplineRole role) => role switch
    {
        DisciplineRole.Teacher => ParticipantRole.Teacher,
        DisciplineRole.Student => ParticipantRole.Student,
        _ => ParticipantRole.Member,
    };
}
