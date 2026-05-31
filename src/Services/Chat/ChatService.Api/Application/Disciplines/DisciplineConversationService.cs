using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Disciplines;

public sealed class DisciplineConversationService(
    IConversationRepository conversations,
    IChatBroadcaster broadcaster,
    ChatEventDispatcher dispatcher,
    ILogger<DisciplineConversationService> logger)
{
    public async Task HandleDisciplineCreatedAsync(
        DisciplineCreatedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var existing = await conversations
            .GetGeneralDisciplineAsync(evt.DisciplineId, cancellationToken)
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
            return;
        }

        var dto = ConversationDto.FromDomain(conversation, evt.OwnerTeacherId);
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

        var updated = await conversations
            .UpdateDisciplineMetadataAsync(evt.DisciplineId, evt.Title, evt.CoverAssetId, cancellationToken)
            .ConfigureAwait(false);
        if (!updated)
        {
            return;
        }

        var disciplineConversations = await conversations
            .ListByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var conversation in disciplineConversations)
        {
            conversation.UpdateDisciplineMetadata(evt.Title, evt.CoverAssetId);
            await broadcaster
                .NotifyConversationUpdatedAsync(
                    conversation.Participants.ToList(),
                    ConversationDto.FromDomain(conversation),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task HandleSubgroupCreatedAsync(
        DisciplineSubgroupCreatedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var existing = await conversations
            .GetByDisciplineSubgroupIdAsync(evt.DisciplineId, evt.SubgroupId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        var conversation = Conversation.OpenDisciplineSubgroup(
            evt.DisciplineId,
            evt.SubgroupId,
            evt.DisciplineTitle,
            evt.Name,
            evt.TeacherUserIds,
            evt.StudentUserIds,
            evt.OccurredAtUtc,
            evt.DisciplineCoverAssetId);
        var created = await conversations.TryCreateAsync(conversation, cancellationToken).ConfigureAwait(false);
        if (!created)
        {
            return;
        }

        foreach (var participantId in conversation.Participants)
        {
            await broadcaster
                .NotifyConversationCreatedAsync(
                    [participantId],
                    ConversationDto.FromDomain(conversation, participantId),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await dispatcher.PublishAsync(
            new ChatDisciplineConversationCreatedEvent(
                conversation.Id,
                evt.DisciplineId,
                evt.TeacherUserIds.Count > 0 ? evt.TeacherUserIds[0] : Guid.Empty,
                evt.DisciplineTitle,
                evt.DisciplineCoverAssetId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleSubgroupUpdatedAsync(
        DisciplineSubgroupUpdatedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var conversation = await conversations
            .GetByDisciplineSubgroupIdAsync(evt.DisciplineId, evt.SubgroupId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var updated = await conversations
            .UpdateSubgroupMetadataAsync(conversation.Id, evt.Name, cancellationToken)
            .ConfigureAwait(false);
        if (!updated)
        {
            return;
        }

        conversation.UpdateSubgroupMetadata(evt.Name);
        await broadcaster
            .NotifyConversationUpdatedAsync(
                conversation.Participants.ToList(),
                ConversationDto.FromDomain(conversation),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task HandleSubgroupArchivedAsync(
        DisciplineSubgroupArchivedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var conversation = await conversations
            .GetByDisciplineSubgroupIdAsync(evt.DisciplineId, evt.SubgroupId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        await ArchiveConversationAsync(conversation, evt.OccurredAtUtc, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleUserEnrolledAsync(
        UserEnrolledEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var general = await conversations
            .GetGeneralDisciplineAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        if (general is not null)
        {
            await AddParticipantAsync(general, evt.UserId, MapRole(evt.Role), cancellationToken).ConfigureAwait(false);
        }

        if (evt.Role == DisciplineRole.Teacher)
        {
            var all = await conversations.ListByDisciplineIdAsync(evt.DisciplineId, cancellationToken).ConfigureAwait(false);
            foreach (var subgroupConversation in all.Where(c => c.DisciplineChatKind == DisciplineChatKind.Subgroup))
            {
                await AddParticipantAsync(subgroupConversation, evt.UserId, ParticipantRole.Teacher, cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        if (evt.SubgroupId is { } subgroupId)
        {
            var subgroup = await conversations
                .GetByDisciplineSubgroupIdAsync(evt.DisciplineId, subgroupId, cancellationToken)
                .ConfigureAwait(false);
            if (subgroup is not null)
            {
                await AddParticipantAsync(subgroup, evt.UserId, ParticipantRole.Student, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task HandleUserUnenrolledAsync(
        UserUnenrolledEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var general = await conversations
            .GetGeneralDisciplineAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        var oldRole = evt.Role.HasValue ? MapRole(evt.Role.Value) : general?.RoleOf(evt.UserId) ?? ParticipantRole.Member;
        if (general is not null)
        {
            await RemoveParticipantAsync(general, evt.UserId, cancellationToken).ConfigureAwait(false);
            if (oldRole == ParticipantRole.Teacher)
            {
                WarnIfLastTeacherLost(general, evt.UserId);
            }
        }

        if (oldRole == ParticipantRole.Teacher)
        {
            var all = await conversations.ListByDisciplineIdAsync(evt.DisciplineId, cancellationToken).ConfigureAwait(false);
            foreach (var subgroupConversation in all.Where(c => c.DisciplineChatKind == DisciplineChatKind.Subgroup))
            {
                await RemoveParticipantAsync(subgroupConversation, evt.UserId, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (evt.SubgroupId is { } subgroupId)
        {
            var subgroup = await conversations
                .GetByDisciplineSubgroupIdAsync(evt.DisciplineId, subgroupId, cancellationToken)
                .ConfigureAwait(false);
            if (subgroup is not null)
            {
                await RemoveParticipantAsync(subgroup, evt.UserId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task HandleEnrollmentRoleChangedAsync(
        EnrollmentRoleChangedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var general = await conversations
            .GetGeneralDisciplineAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        var newRole = MapRole(evt.NewRole);
        if (general is not null)
        {
            var changed = await conversations
                .ChangeParticipantRoleAsync(general.Id, evt.UserId, newRole, cancellationToken)
                .ConfigureAwait(false);
            if (changed)
            {
                await broadcaster
                    .NotifyParticipantRoleChangedAsync(general.Participants.ToList(), general.Id, evt.UserId, newRole, cancellationToken)
                    .ConfigureAwait(false);

                await dispatcher.PublishAsync(
                    new ChatParticipantRoleChangedEvent(
                        general.Id,
                        evt.UserId,
                        MapToContract(MapRole(evt.OldRole)),
                        MapToContract(newRole)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (evt.OldRole == DisciplineRole.Student && evt.OldSubgroupId is { } oldSubgroupId)
        {
            await RemoveFromSubgroupAsync(evt.DisciplineId, oldSubgroupId, evt.UserId, cancellationToken).ConfigureAwait(false);
        }

        var all = await conversations.ListByDisciplineIdAsync(evt.DisciplineId, cancellationToken).ConfigureAwait(false);
        if (evt.NewRole == DisciplineRole.Teacher)
        {
            foreach (var subgroupConversation in all.Where(c => c.DisciplineChatKind == DisciplineChatKind.Subgroup))
            {
                await AddParticipantAsync(subgroupConversation, evt.UserId, ParticipantRole.Teacher, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            foreach (var subgroupConversation in all.Where(c => c.DisciplineChatKind == DisciplineChatKind.Subgroup))
            {
                await RemoveParticipantAsync(subgroupConversation, evt.UserId, cancellationToken).ConfigureAwait(false);
            }

            if (evt.NewSubgroupId is { } newSubgroupId)
            {
                await AddToSubgroupAsync(evt.DisciplineId, newSubgroupId, evt.UserId, ParticipantRole.Student, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    public async Task HandleEnrollmentSubgroupChangedAsync(
        EnrollmentSubgroupChangedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (evt.OldSubgroupId is { } oldSubgroupId)
        {
            await RemoveFromSubgroupAsync(evt.DisciplineId, oldSubgroupId, evt.UserId, cancellationToken).ConfigureAwait(false);
        }

        if (evt.NewSubgroupId is { } newSubgroupId)
        {
            await AddToSubgroupAsync(evt.DisciplineId, newSubgroupId, evt.UserId, ParticipantRole.Student, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task HandleDisciplineDeletedAsync(
        DisciplineDeletedEvent evt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var disciplineConversations = await conversations
            .ListByDisciplineIdAsync(evt.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var conversation in disciplineConversations)
        {
            await ArchiveConversationAsync(conversation, evt.OccurredAtUtc, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AddToSubgroupAsync(
        Guid disciplineId,
        Guid subgroupId,
        Guid userId,
        ParticipantRole role,
        CancellationToken cancellationToken)
    {
        var subgroup = await conversations
            .GetByDisciplineSubgroupIdAsync(disciplineId, subgroupId, cancellationToken)
            .ConfigureAwait(false);
        if (subgroup is not null)
        {
            await AddParticipantAsync(subgroup, userId, role, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RemoveFromSubgroupAsync(
        Guid disciplineId,
        Guid subgroupId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var subgroup = await conversations
            .GetByDisciplineSubgroupIdAsync(disciplineId, subgroupId, cancellationToken)
            .ConfigureAwait(false);
        if (subgroup is not null)
        {
            await RemoveParticipantAsync(subgroup, userId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AddParticipantAsync(
        Conversation conversation,
        Guid userId,
        ParticipantRole role,
        CancellationToken cancellationToken)
    {
        var added = await conversations
            .AddParticipantAsync(conversation.Id, userId, role, cancellationToken)
            .ConfigureAwait(false);
        if (!added)
        {
            return;
        }

        var existingParticipants = conversation.Participants.ToList();
        if (existingParticipants.Count > 0)
        {
            await broadcaster
                .NotifyParticipantJoinedAsync(existingParticipants, conversation.Id, userId, role, cancellationToken)
                .ConfigureAwait(false);
        }

        conversation.AddParticipant(userId, role);
        await broadcaster
            .NotifyConversationCreatedAsync([userId], ConversationDto.FromDomain(conversation, userId), cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatParticipantJoinedEvent(conversation.Id, userId, MapToContract(role)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveParticipantAsync(
        Conversation conversation,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var removed = await conversations
            .RemoveParticipantAsync(conversation.Id, userId, cancellationToken)
            .ConfigureAwait(false);
        if (!removed)
        {
            return;
        }

        await broadcaster
            .NotifyParticipantLeftAsync(conversation.Participants.ToList(), conversation.Id, userId, cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatParticipantLeftEvent(conversation.Id, userId),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ArchiveConversationAsync(
        Conversation conversation,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        var archived = await conversations
            .ArchiveAsync(conversation.Id, archivedAtUtc, cancellationToken)
            .ConfigureAwait(false);
        if (!archived)
        {
            return;
        }

        await broadcaster
            .NotifyConversationArchivedAsync(conversation.Participants.ToList(), conversation.Id, archivedAtUtc, cancellationToken)
            .ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatConversationArchivedEvent(conversation.Id, archivedAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

    private void WarnIfLastTeacherLost(Conversation conversation, Guid userIdLeavingTeacherRole)
    {
        var remainingTeachers = conversation.ParticipantRoles
            .Where(kv => kv.Key != userIdLeavingTeacherRole && kv.Value == ParticipantRole.Teacher)
            .Count();
        if (remainingTeachers == 0)
        {
            logger.LogWarning(
                "Discipline conversation {ConversationId} has no Teacher participants left after {UserId} stepped down. " +
                "DisciplineService is expected to enforce the ≥1 Teacher invariant — this is a drift signal.",
                conversation.Id,
                userIdLeavingTeacherRole);
        }
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
