using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Disciplines;

public sealed record DisciplineConversationReconciliationReport(
    int PagesScanned,
    int DisciplinesScanned,
    int SubgroupsScanned,
    int ConversationsCreated,
    int ConversationsRestored,
    int ConversationsArchived,
    int MetadataUpdated,
    int ParticipantsAdded,
    int ParticipantsRemoved,
    int ParticipantRolesUpdated,
    int SkippedDisciplines);

public sealed class DisciplineConversationReconciliationService(
    IDisciplineServiceClient disciplineService,
    IConversationRepository conversations,
    ChatEventDispatcher dispatcher,
    TimeProvider timeProvider,
    ILogger<DisciplineConversationReconciliationService> logger)
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 500;

    public async Task<DisciplineConversationReconciliationReport> ReconcileAsync(
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPageSize = NormalizePageSize(pageSize);
        var report = new MutableReport();
        string? pageToken = null;

        do
        {
            var page = await disciplineService
                .ListDisciplineSnapshotsAsync(
                    pageToken,
                    normalizedPageSize,
                    includeArchived: true,
                    cancellationToken)
                .ConfigureAwait(false);
            report.PagesScanned++;

            foreach (var snapshot in page.Items)
            {
                await ReconcileDisciplineAsync(snapshot, report, cancellationToken).ConfigureAwait(false);
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return report.ToReport();
    }

    private async Task ReconcileDisciplineAsync(
        DisciplineSnapshot snapshot,
        MutableReport report,
        CancellationToken cancellationToken)
    {
        report.DisciplinesScanned++;
        report.SubgroupsScanned += snapshot.Subgroups.Count;

        var localConversations = await conversations
            .ListByDisciplineIdAsync(snapshot.DisciplineId, cancellationToken)
            .ConfigureAwait(false);

        if (await conversations
                .UpdateDisciplineMetadataAsync(
                    snapshot.DisciplineId,
                    snapshot.Title,
                    snapshot.CoverAssetId,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            report.MetadataUpdated++;
        }

        if (snapshot.IsArchived)
        {
            foreach (var conversation in localConversations)
            {
                await ArchiveConversationAsync(
                    conversation,
                    snapshot.ArchivedAtUtc ?? timeProvider.GetUtcNow(),
                    report,
                    cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var teachers = snapshot.Members
            .Where(m => m.Role == ParticipantRole.Teacher)
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        var general = FindGeneralConversation(localConversations)
            ?? await EnsureGeneralConversationAsync(snapshot, teachers, report, cancellationToken).ConfigureAwait(false);
        if (general is null)
        {
            return;
        }

        await RestoreIfArchivedAsync(general, report, cancellationToken).ConfigureAwait(false);
        await SyncConversationParticipantsAsync(
            general,
            ExpectedGeneralParticipants(snapshot),
            report,
            cancellationToken).ConfigureAwait(false);

        var activeSubgroupIds = snapshot.Subgroups
            .Where(s => !s.ArchivedAtUtc.HasValue)
            .Select(s => s.SubgroupId)
            .ToHashSet();

        foreach (var subgroup in snapshot.Subgroups)
        {
            var existing = FindSubgroupConversation(localConversations, subgroup.SubgroupId);
            if (subgroup.ArchivedAtUtc.HasValue)
            {
                if (existing is not null)
                {
                    await ArchiveConversationAsync(existing, subgroup.ArchivedAtUtc.Value, report, cancellationToken)
                        .ConfigureAwait(false);
                }

                continue;
            }

            var subgroupConversation = existing
                ?? await EnsureSubgroupConversationAsync(snapshot, subgroup, teachers, report, cancellationToken)
                    .ConfigureAwait(false);
            if (subgroupConversation is null)
            {
                continue;
            }

            await RestoreIfArchivedAsync(subgroupConversation, report, cancellationToken).ConfigureAwait(false);
            if (await conversations
                    .UpdateSubgroupMetadataAsync(
                        subgroupConversation.Id,
                        subgroup.Name,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                report.MetadataUpdated++;
            }

            await SyncConversationParticipantsAsync(
                subgroupConversation,
                ExpectedSubgroupParticipants(snapshot, subgroup.SubgroupId, teachers),
                report,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var staleSubgroupConversation in localConversations
                     .Where(c => c.DisciplineChatKind == DisciplineChatKind.Subgroup
                         && c.DisciplineSubgroupId is { } subgroupId
                         && !activeSubgroupIds.Contains(subgroupId)))
        {
            await ArchiveConversationAsync(
                staleSubgroupConversation,
                timeProvider.GetUtcNow(),
                report,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Conversation?> EnsureGeneralConversationAsync(
        DisciplineSnapshot snapshot,
        List<Guid> teachers,
        MutableReport report,
        CancellationToken cancellationToken)
    {
        var ownerTeacherId = snapshot.OwnerTeacherId != Guid.Empty
            ? snapshot.OwnerTeacherId
            : (teachers.Count > 0 ? teachers[0] : Guid.Empty);
        if (ownerTeacherId == Guid.Empty)
        {
            report.SkippedDisciplines++;
            logger.LogWarning(
                "Discipline conversation reconciliation skipped {DisciplineId}: no owner teacher or teacher member.",
                snapshot.DisciplineId);
            return null;
        }

        var conversation = Conversation.OpenDiscipline(
            snapshot.DisciplineId,
            ownerTeacherId,
            snapshot.CreatedAtUtc,
            snapshot.Title,
            snapshot.CoverAssetId);
        var created = await conversations.TryCreateAsync(conversation, cancellationToken).ConfigureAwait(false);
        if (created)
        {
            report.ConversationsCreated++;
            await dispatcher.PublishAsync(
                new ChatDisciplineConversationCreatedEvent(
                    conversation.Id,
                    snapshot.DisciplineId,
                    ownerTeacherId,
                    snapshot.Title,
                    snapshot.CoverAssetId),
                cancellationToken).ConfigureAwait(false);
            return conversation;
        }

        return await conversations
            .GetGeneralDisciplineAsync(snapshot.DisciplineId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Conversation?> EnsureSubgroupConversationAsync(
        DisciplineSnapshot snapshot,
        DisciplineSubgroupSnapshot subgroup,
        List<Guid> teachers,
        MutableReport report,
        CancellationToken cancellationToken)
    {
        var studentIds = snapshot.Members
            .Where(m => m.Role == ParticipantRole.Student && m.SubgroupId == subgroup.SubgroupId)
            .Select(m => m.UserId)
            .Distinct()
            .ToList();
        var conversation = Conversation.OpenDisciplineSubgroup(
            snapshot.DisciplineId,
            subgroup.SubgroupId,
            snapshot.Title,
            subgroup.Name,
            teachers,
            studentIds,
            subgroup.CreatedAtUtc,
            snapshot.CoverAssetId);

        var created = await conversations.TryCreateAsync(conversation, cancellationToken).ConfigureAwait(false);
        if (created)
        {
            report.ConversationsCreated++;
            await dispatcher.PublishAsync(
                new ChatDisciplineConversationCreatedEvent(
                    conversation.Id,
                    snapshot.DisciplineId,
                    snapshot.OwnerTeacherId,
                    snapshot.Title,
                    snapshot.CoverAssetId),
                cancellationToken).ConfigureAwait(false);
            return conversation;
        }

        return await conversations
            .GetByDisciplineSubgroupIdAsync(snapshot.DisciplineId, subgroup.SubgroupId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SyncConversationParticipantsAsync(
        Conversation conversation,
        Dictionary<Guid, ParticipantRole> expectedParticipants,
        MutableReport report,
        CancellationToken cancellationToken)
    {
        var currentParticipants = conversation.Participants.ToHashSet();
        foreach (var userId in currentParticipants.Where(userId => !expectedParticipants.ContainsKey(userId)))
        {
            if (await conversations
                    .RemoveParticipantAsync(conversation.Id, userId, cancellationToken)
                    .ConfigureAwait(false))
            {
                report.ParticipantsRemoved++;
            }
        }

        foreach (var (userId, expectedRole) in expectedParticipants)
        {
            if (!currentParticipants.Contains(userId))
            {
                var added = await conversations
                    .AddParticipantAsync(conversation.Id, userId, expectedRole, cancellationToken)
                    .ConfigureAwait(false);
                if (!added)
                {
                    added = await conversations
                        .EnsureParticipantAsync(conversation.Id, userId, expectedRole, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (added)
                {
                    report.ParticipantsAdded++;
                }

                continue;
            }

            if (conversation.RoleOf(userId) != expectedRole
                && await conversations
                    .ChangeParticipantRoleAsync(conversation.Id, userId, expectedRole, cancellationToken)
                    .ConfigureAwait(false))
            {
                report.ParticipantRolesUpdated++;
            }
        }
    }

    private async Task ArchiveConversationAsync(
        Conversation conversation,
        DateTimeOffset archivedAtUtc,
        MutableReport report,
        CancellationToken cancellationToken)
    {
        if (conversation.IsArchived)
        {
            return;
        }

        if (await conversations.ArchiveAsync(conversation.Id, archivedAtUtc, cancellationToken).ConfigureAwait(false))
        {
            report.ConversationsArchived++;
        }
    }

    private async Task RestoreIfArchivedAsync(
        Conversation conversation,
        MutableReport report,
        CancellationToken cancellationToken)
    {
        if (!conversation.IsArchived)
        {
            return;
        }

        if (await conversations.UnarchiveAsync(conversation.Id, cancellationToken).ConfigureAwait(false))
        {
            report.ConversationsRestored++;
        }
    }

    private static Dictionary<Guid, ParticipantRole> ExpectedGeneralParticipants(DisciplineSnapshot snapshot)
        => snapshot.Members
            .Where(m => m.Role is ParticipantRole.Teacher or ParticipantRole.Student)
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.Last().Role);

    private static Dictionary<Guid, ParticipantRole> ExpectedSubgroupParticipants(
        DisciplineSnapshot snapshot,
        Guid subgroupId,
        List<Guid> teachers)
    {
        var result = teachers.ToDictionary(id => id, _ => ParticipantRole.Teacher);
        foreach (var student in snapshot.Members
                     .Where(m => m.Role == ParticipantRole.Student && m.SubgroupId == subgroupId)
                     .GroupBy(m => m.UserId)
                     .Select(g => g.Last()))
        {
            result[student.UserId] = ParticipantRole.Student;
        }

        return result;
    }

    private static Conversation? FindGeneralConversation(IReadOnlyList<Conversation> conversations)
        => conversations.FirstOrDefault(c =>
            c.DisciplineSubgroupId is null
            && (c.DisciplineChatKind is null or DisciplineChatKind.General));

    private static Conversation? FindSubgroupConversation(
        IReadOnlyList<Conversation> conversations,
        Guid subgroupId)
        => conversations.FirstOrDefault(c => c.DisciplineSubgroupId == subgroupId);

    private static int NormalizePageSize(int pageSize) => pageSize switch
    {
        <= 0 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => pageSize,
    };

    private sealed class MutableReport
    {
        public int PagesScanned { get; set; }

        public int DisciplinesScanned { get; set; }

        public int SubgroupsScanned { get; set; }

        public int ConversationsCreated { get; set; }

        public int ConversationsRestored { get; set; }

        public int ConversationsArchived { get; set; }

        public int MetadataUpdated { get; set; }

        public int ParticipantsAdded { get; set; }

        public int ParticipantsRemoved { get; set; }

        public int ParticipantRolesUpdated { get; set; }

        public int SkippedDisciplines { get; set; }

        public DisciplineConversationReconciliationReport ToReport()
            => new(
                PagesScanned,
                DisciplinesScanned,
                SubgroupsScanned,
                ConversationsCreated,
                ConversationsRestored,
                ConversationsArchived,
                MetadataUpdated,
                ParticipantsAdded,
                ParticipantsRemoved,
                ParticipantRolesUpdated,
                SkippedDisciplines);
    }
}
