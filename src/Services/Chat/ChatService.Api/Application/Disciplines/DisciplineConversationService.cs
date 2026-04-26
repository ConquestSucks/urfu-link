using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Disciplines;

/// <summary>
/// Reacts to discipline integration events by keeping the corresponding group conversation
/// in sync. All operations are idempotent: the discipline-events topic is at-least-once,
/// so each handler must tolerate replays.
/// </summary>
public sealed class DisciplineConversationService(IConversationRepository conversations)
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
        await conversations.TryCreateAsync(conversation, cancellationToken).ConfigureAwait(false);
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

        await conversations
            .UpdateMetadataAsync(conversation.Id, evt.Title, evt.CoverAssetId, cancellationToken)
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

        await conversations
            .AddParticipantAsync(conversation.Id, evt.UserId, MapRole(evt.Role), cancellationToken)
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

        await conversations
            .RemoveParticipantAsync(conversation.Id, evt.UserId, cancellationToken)
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

        await conversations
            .ChangeParticipantRoleAsync(conversation.Id, evt.UserId, MapRole(evt.NewRole), cancellationToken)
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

        await conversations
            .ArchiveAsync(conversation.Id, evt.OccurredAtUtc, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ParticipantRole MapRole(DisciplineRole role) => role switch
    {
        DisciplineRole.Teacher => ParticipantRole.Teacher,
        DisciplineRole.Student => ParticipantRole.Student,
        _ => ParticipantRole.Member,
    };
}
