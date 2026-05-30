using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Discipline;

[Collection(IntegrationCollection.Name)]
public sealed class DisciplineConversationReconciliationTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;
    private readonly List<IServiceScope> _scopes = [];

    public DisciplineConversationReconciliationTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        _scopes.Clear();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ReconcileAsync_CreatesGeneralAndSubgroupConversationsFromSnapshot()
    {
        var disciplineId = Guid.NewGuid();
        var subgroupId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        _factory.DisciplineServiceClient.SeedSnapshots(Snapshot(
            disciplineId,
            teacherId,
            subgroupId,
            new DisciplineMember(teacherId, ParticipantRole.Teacher),
            new DisciplineMember(studentId, ParticipantRole.Student, subgroupId)));

        var report = await ResolveReconciliation().ReconcileAsync(50, CancellationToken.None);

        report.ConversationsCreated.Should().Be(2);
        report.ParticipantsAdded.Should().Be(1);
        var general = await ResolveRepo().GetGeneralDisciplineAsync(disciplineId, CancellationToken.None);
        general.Should().NotBeNull();
        general!.Participants.Should().BeEquivalentTo(new[] { teacherId, studentId });
        general.RoleOf(teacherId).Should().Be(ParticipantRole.Teacher);
        general.RoleOf(studentId).Should().Be(ParticipantRole.Student);

        var subgroup = await ResolveRepo().GetByDisciplineSubgroupIdAsync(
            disciplineId,
            subgroupId,
            CancellationToken.None);
        subgroup.Should().NotBeNull();
        subgroup!.Participants.Should().BeEquivalentTo(new[] { teacherId, studentId });
        subgroup.DisciplineChatKind.Should().Be(DisciplineChatKind.Subgroup);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatDisciplineConversationCreatedEvent>()
            .Should().HaveCount(2)
            .And.AllSatisfy(e => e.DisciplineId.Should().Be(disciplineId));

        var secondReport = await ResolveReconciliation().ReconcileAsync(50, CancellationToken.None);
        secondReport.ConversationsCreated.Should().Be(0);
        secondReport.ParticipantsAdded.Should().Be(0);
        secondReport.ParticipantsRemoved.Should().Be(0);
        secondReport.ParticipantRolesUpdated.Should().Be(0);
    }

    [Fact]
    public async Task ReconcileAsync_RepairsParticipantArrayWhenRoleEntryAlreadyExists()
    {
        var disciplineId = Guid.NewGuid();
        var subgroupId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var eventService = ResolveEventService();
        await eventService.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS201", "Databases", null, "2026", teacherId, null),
            CancellationToken.None);
        await eventService.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId, subgroupId),
            CancellationToken.None);
        await RemoveParticipantArrayEntryOnlyAsync(Conversation.ComputeDisciplineId(disciplineId), studentId);

        _factory.DisciplineServiceClient.SeedSnapshots(Snapshot(
            disciplineId,
            teacherId,
            subgroupId,
            new DisciplineMember(teacherId, ParticipantRole.Teacher),
            new DisciplineMember(studentId, ParticipantRole.Student, subgroupId)));

        var report = await ResolveReconciliation().ReconcileAsync(50, CancellationToken.None);

        report.ParticipantsAdded.Should().Be(1);
        var general = await ResolveRepo().GetGeneralDisciplineAsync(disciplineId, CancellationToken.None);
        general!.Participants.Should().Contain(studentId);
        general.RoleOf(studentId).Should().Be(ParticipantRole.Student);
    }

    [Fact]
    public async Task ReconcileAsync_RepairsParticipantsAndArchivesClosedSubgroups()
    {
        var disciplineId = Guid.NewGuid();
        var activeSubgroupId = Guid.NewGuid();
        var archivedSubgroupId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var oldStudentId = Guid.NewGuid();
        var newStudentId = Guid.NewGuid();
        var eventService = ResolveEventService();
        await eventService.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS200", "Algorithms", null, "2026", teacherId, null),
            CancellationToken.None);
        await eventService.HandleSubgroupCreatedAsync(
            new DisciplineSubgroupCreatedEvent(
                disciplineId,
                activeSubgroupId,
                "Algorithms",
                null,
                "Group A",
                [teacherId],
                [oldStudentId]),
            CancellationToken.None);
        await eventService.HandleSubgroupCreatedAsync(
            new DisciplineSubgroupCreatedEvent(
                disciplineId,
                archivedSubgroupId,
                "Algorithms",
                null,
                "Group B",
                [teacherId],
                []),
            CancellationToken.None);

        var archivedAt = DateTimeOffset.UtcNow;
        _factory.DisciplineServiceClient.SeedSnapshots(Snapshot(
            disciplineId,
            teacherId,
            activeSubgroupId,
            [
                new DisciplineMember(teacherId, ParticipantRole.Teacher),
                new DisciplineMember(newStudentId, ParticipantRole.Student, activeSubgroupId),
            ],
            archivedSubgroupId,
            archivedAt));

        var report = await ResolveReconciliation().ReconcileAsync(50, CancellationToken.None);

        report.ParticipantsAdded.Should().Be(2);
        report.ParticipantsRemoved.Should().Be(1);
        report.ConversationsArchived.Should().Be(1);

        var general = await ResolveRepo().GetGeneralDisciplineAsync(disciplineId, CancellationToken.None);
        general!.Participants.Should().BeEquivalentTo(new[] { teacherId, newStudentId });

        var active = await ResolveRepo().GetByDisciplineSubgroupIdAsync(
            disciplineId,
            activeSubgroupId,
            CancellationToken.None);
        active!.Participants.Should().BeEquivalentTo(new[] { teacherId, newStudentId });
        active.IsArchived.Should().BeFalse();

        var archived = await ResolveRepo().GetByDisciplineSubgroupIdAsync(
            disciplineId,
            archivedSubgroupId,
            CancellationToken.None);
        archived!.IsArchived.Should().BeTrue();
    }

    private DisciplineConversationReconciliationService ResolveReconciliation()
    {
        var scope = _factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<DisciplineConversationReconciliationService>();
    }

    private DisciplineConversationService ResolveEventService()
    {
        var scope = _factory.Services.CreateScope();
        _scopes.Add(scope);
        var repo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Urfu.Link.Services.Chat.Application.ChatEventDispatcher>();
        var logger = scope.ServiceProvider.GetRequiredService<
            Microsoft.Extensions.Logging.ILogger<DisciplineConversationService>>();
        return new DisciplineConversationService(repo, _factory.ChatBroadcaster, dispatcher, logger);
    }

    private IConversationRepository ResolveRepo()
    {
        var scope = _factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IConversationRepository>();
    }

    private async Task RemoveParticipantArrayEntryOnlyAsync(string conversationId, Guid userId)
    {
        var scope = _factory.Services.CreateScope();
        _scopes.Add(scope);
        var context = scope.ServiceProvider.GetRequiredService<ChatMongoContext>();
        var collection = context.Database.GetCollection<BsonDocument>(ChatMongoContext.ConversationsCollectionName);
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", conversationId),
            Builders<BsonDocument>.Update.Pull(
                "participants",
                new BsonBinaryData(userId, GuidRepresentation.Standard)));
    }

    private static DisciplineSnapshot Snapshot(
        Guid disciplineId,
        Guid teacherId,
        Guid subgroupId,
        params DisciplineMember[] members)
        => Snapshot(disciplineId, teacherId, subgroupId, members, archivedSubgroupId: null, archivedAt: null);

    private static DisciplineSnapshot Snapshot(
        Guid disciplineId,
        Guid teacherId,
        Guid activeSubgroupId,
        IReadOnlyList<DisciplineMember> members,
        Guid? archivedSubgroupId,
        DateTimeOffset? archivedAt)
    {
        var now = DateTimeOffset.UtcNow;
        var subgroups = new List<DisciplineSubgroupSnapshot>
        {
            new(activeSubgroupId, "Group A", now, now, null),
        };
        if (archivedSubgroupId.HasValue)
        {
            subgroups.Add(new DisciplineSubgroupSnapshot(
                archivedSubgroupId.Value,
                "Group B",
                now,
                archivedAt ?? now,
                archivedAt ?? now));
        }

        return new DisciplineSnapshot(
            disciplineId,
            "CS200",
            "Algorithms",
            "2026",
            teacherId,
            null,
            now,
            now,
            null,
            subgroups,
            members);
    }
}
