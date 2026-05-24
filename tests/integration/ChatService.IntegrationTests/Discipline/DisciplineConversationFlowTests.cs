using System.Diagnostics;
using System.Text.Json;
using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Messaging;

namespace ChatService.IntegrationTests.Discipline;

[Collection(IntegrationCollection.Name)]
public sealed class DisciplineConversationFlowTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public DisciplineConversationFlowTests(ChatServiceFactory factory)
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
        _consumer?.Dispose();
        _consumer = null;
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DisciplineCreated_OpensGroupConversationWithOwnerTeacher()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS101", "Intro", null, "2026", teacherId, null),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv.Should().NotBeNull();
        conv!.Type.Should().Be(ConversationType.Group);
        conv.Participants.Should().ContainSingle().Which.Should().Be(teacherId);
        conv.IsTeacher(teacherId).Should().BeTrue();
    }

    [Fact]
    public async Task DisciplineCreated_TwiceWithSameId_IsIdempotent()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var service = ResolveService();
        var evt = new DisciplineCreatedEvent(disciplineId, "CS102", "X", null, "2026", teacherId, null);

        await service.HandleDisciplineCreatedAsync(evt, CancellationToken.None);
        await service.HandleDisciplineCreatedAsync(evt, CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv.Should().NotBeNull();
        conv!.Participants.Should().HaveCount(1);
    }

    [Fact]
    public async Task UserEnrolled_AddsParticipantWithRole()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS103", "Y", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv!.IsParticipant(studentId).Should().BeTrue();
        conv.RoleOf(studentId).Should().Be(ParticipantRole.Student);
    }

    [Fact]
    public async Task UserUnenrolled_RemovesParticipant()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS104", "Z", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);
        await service.HandleUserUnenrolledAsync(
            new UserUnenrolledEvent(disciplineId, studentId),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv!.IsParticipant(studentId).Should().BeFalse();
        conv.RoleOf(studentId).Should().Be(ParticipantRole.Member);
    }

    [Fact]
    public async Task EnrollmentRoleChanged_PromotesStudent()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS105", "W", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);
        await service.HandleEnrollmentRoleChangedAsync(
            new EnrollmentRoleChangedEvent(disciplineId, studentId, DisciplineRole.Student, DisciplineRole.Teacher),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv!.IsTeacher(studentId).Should().BeTrue();
    }

    [Fact]
    public async Task DisciplineCreated_StoresTitleAndCoverFromEvent()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var coverId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS210", "Compilers", null, "2026", teacherId, coverId),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv!.Title.Should().Be("Compilers");
        conv.CoverAssetId.Should().Be(coverId);
        conv.GroupSubtype.Should().Be(GroupSubtype.Discipline);
    }

    [Fact]
    public async Task DisciplineUpdated_RefreshesTitleAndCover()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var initialCover = Guid.NewGuid();
        var newCover = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS220", "Algorithms I", null, "2026", teacherId, initialCover),
            CancellationToken.None);
        await service.HandleDisciplineUpdatedAsync(
            new DisciplineUpdatedEvent(disciplineId, "CS220", "Algorithms (renamed)", "desc", "2026", teacherId, newCover),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv!.Title.Should().Be("Algorithms (renamed)");
        conv.CoverAssetId.Should().Be(newCover);
    }

    [Fact]
    public async Task DisciplineUpdated_OnUnknownDiscipline_IsNoOp()
    {
        var service = ResolveService();

        var act = () => service.HandleDisciplineUpdatedAsync(
            new DisciplineUpdatedEvent(Guid.NewGuid(), "X", "Title", null, "2026", Guid.NewGuid(), null),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisciplineDeleted_ArchivesConversation()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "CS106", "V", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleDisciplineDeletedAsync(
            new DisciplineDeletedEvent(disciplineId),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task DisciplineCreated_BroadcastsConversationCreatedToOwner()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "BC1", "Broadcast test", null, "2026", teacherId, null),
            CancellationToken.None);

        var broadcasts = _factory.ChatBroadcaster.ConversationCreated;
        broadcasts.Should().ContainSingle()
            .Which.Recipients.Should().ContainSingle().Which.Should().Be(teacherId);
    }

    [Fact]
    public async Task UserEnrolled_BroadcastsParticipantJoined_AndConversationCreatedForNewUser()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "BC2", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        _factory.ChatBroadcaster.Reset();

        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);

        _factory.ChatBroadcaster.ParticipantJoined.Should().ContainSingle()
            .Which.Should().Match<FakeBroadcastParticipantJoinedRecord>(r =>
                r.UserId == studentId
                && r.Role == ParticipantRole.Student
                && r.Recipients.Count == 1
                && r.Recipients[0] == teacherId);
        _factory.ChatBroadcaster.ConversationCreated.Should().ContainSingle()
            .Which.Recipients.Should().ContainSingle().Which.Should().Be(studentId);
    }

    [Fact]
    public async Task UserUnenrolled_BroadcastsParticipantLeftToAllPriorParticipants()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "BC3", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);
        _factory.ChatBroadcaster.Reset();

        await service.HandleUserUnenrolledAsync(
            new UserUnenrolledEvent(disciplineId, studentId),
            CancellationToken.None);

        var record = _factory.ChatBroadcaster.ParticipantLeft.Should().ContainSingle().Which;
        record.UserId.Should().Be(studentId);
        record.Recipients.Should().BeEquivalentTo(new[] { teacherId, studentId });
    }

    [Fact]
    public async Task EnrollmentRoleChanged_BroadcastsRoleChangedToAllParticipants()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "BC4", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);
        _factory.ChatBroadcaster.Reset();

        await service.HandleEnrollmentRoleChangedAsync(
            new EnrollmentRoleChangedEvent(disciplineId, studentId, DisciplineRole.Student, DisciplineRole.Teacher),
            CancellationToken.None);

        var record = _factory.ChatBroadcaster.ParticipantRoleChanged.Should().ContainSingle().Which;
        record.UserId.Should().Be(studentId);
        record.NewRole.Should().Be(ParticipantRole.Teacher);
        record.Recipients.Should().BeEquivalentTo(new[] { teacherId, studentId });
    }

    [Fact]
    public async Task DisciplineCreated_PublishesChatDisciplineConversationCreated()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var coverId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "PUB1", "Pub", null, "2026", teacherId, coverId),
            CancellationToken.None);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatDisciplineConversationCreatedEvent>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatDisciplineConversationCreatedEvent>(e =>
                e.DisciplineId == disciplineId
                && e.OwnerTeacherId == teacherId
                && e.Title == "Pub"
                && e.CoverAssetId == coverId);
    }

    [Fact]
    public async Task UserEnrolled_PublishesChatParticipantJoined()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "PUB2", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        _factory.OutboxWriter.Clear();

        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatParticipantJoinedEvent>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatParticipantJoinedEvent>(e =>
                e.UserId == studentId && e.Role == ChatParticipantRole.Student);
    }

    [Fact]
    public async Task UserUnenrolled_PublishesChatParticipantLeft()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "PUB3", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);
        _factory.OutboxWriter.Clear();

        await service.HandleUserUnenrolledAsync(
            new UserUnenrolledEvent(disciplineId, studentId),
            CancellationToken.None);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatParticipantLeftEvent>()
            .Should().ContainSingle()
            .Which.UserId.Should().Be(studentId);
    }

    [Fact]
    public async Task EnrollmentRoleChanged_PublishesChatParticipantRoleChanged()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "PUB4", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);
        _factory.OutboxWriter.Clear();

        await service.HandleEnrollmentRoleChangedAsync(
            new EnrollmentRoleChangedEvent(disciplineId, studentId, DisciplineRole.Student, DisciplineRole.Teacher),
            CancellationToken.None);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatParticipantRoleChangedEvent>()
            .Should().ContainSingle()
            .Which.Should().Match<ChatParticipantRoleChangedEvent>(e =>
                e.UserId == studentId
                && e.OldRole == ChatParticipantRole.Student
                && e.NewRole == ChatParticipantRole.Teacher);
    }

    [Fact]
    public async Task DisciplineDeleted_PublishesChatConversationArchived()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "PUB5", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        _factory.OutboxWriter.Clear();

        await service.HandleDisciplineDeletedAsync(
            new DisciplineDeletedEvent(disciplineId),
            CancellationToken.None);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatConversationArchivedEvent>()
            .Should().ContainSingle();
    }

    [Fact]
    public async Task DisciplineDeleted_BroadcastsConversationArchivedToAllParticipants()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var service = ResolveService();

        await service.HandleDisciplineCreatedAsync(
            new DisciplineCreatedEvent(disciplineId, "BC5", "x", null, "2026", teacherId, null),
            CancellationToken.None);
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId),
            CancellationToken.None);
        _factory.ChatBroadcaster.Reset();

        await service.HandleDisciplineDeletedAsync(
            new DisciplineDeletedEvent(disciplineId),
            CancellationToken.None);

        _factory.ChatBroadcaster.ConversationArchived.Should().ContainSingle()
            .Which.Recipients.Should().BeEquivalentTo(new[] { teacherId, studentId });
    }

    [Fact]
    public async Task UserEnrolled_WithoutPrecedingDisciplineCreated_IsNoOp()
    {
        // Out-of-order delivery: enrollment arrives before the create. Since at-least-once
        // is the only guarantee, the consumer should drop the orphan event silently rather
        // than synthesising a conversation with no owner.
        var service = ResolveService();
        await service.HandleUserEnrolledAsync(
            new UserEnrolledEvent(Guid.NewGuid(), Guid.NewGuid(), DisciplineRole.Student, Guid.NewGuid()),
            CancellationToken.None);

        // No exception is the assertion here.
    }

    [Fact]
    public async Task DispatchAsync_OnDuplicateMessageId_ProcessesOnlyOnce()
    {
        var consumer = ResolveConsumer();
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var envelope = BuildEnvelope(
            new DisciplineCreatedEvent(disciplineId, "CS-DUP", "Dup", null, "2026", teacherId, null));

        await consumer.DispatchAsync(envelope, CancellationToken.None);
        await consumer.DispatchAsync(envelope, CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv.Should().NotBeNull();
        conv!.Participants.Should().ContainSingle();
    }

    [Fact]
    public async Task DispatchAsync_RoutesAllSixEventTypes()
    {
        var consumer = ResolveConsumer();
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        await consumer.DispatchAsync(
            BuildEnvelope(new DisciplineCreatedEvent(disciplineId, "CS-FULL", "Full", null, "2026", teacherId, null)),
            CancellationToken.None);
        await consumer.DispatchAsync(
            BuildEnvelope(new DisciplineUpdatedEvent(disciplineId, "CS-FULL", "Renamed", null, "2026", teacherId, null)),
            CancellationToken.None);
        await consumer.DispatchAsync(
            BuildEnvelope(new UserEnrolledEvent(disciplineId, studentId, DisciplineRole.Student, teacherId)),
            CancellationToken.None);
        await consumer.DispatchAsync(
            BuildEnvelope(new EnrollmentRoleChangedEvent(disciplineId, studentId, DisciplineRole.Student, DisciplineRole.Teacher)),
            CancellationToken.None);
        await consumer.DispatchAsync(
            BuildEnvelope(new UserUnenrolledEvent(disciplineId, studentId)),
            CancellationToken.None);
        await consumer.DispatchAsync(
            BuildEnvelope(new DisciplineDeletedEvent(disciplineId)),
            CancellationToken.None);

        var conv = await ResolveRepo().GetByDisciplineIdAsync(disciplineId, CancellationToken.None);
        conv!.IsArchived.Should().BeTrue();
        conv.IsParticipant(studentId).Should().BeFalse();
    }

    private static readonly JsonSerializerOptions EnvelopeJsonOptions =
        new(JsonSerializerDefaults.Web);

    private DisciplineConversationService ResolveService()
    {
        // Build the service against the captured-by-fake broadcaster instead of the production
        // SignalR-backed one — domain-flow tests assert on the broadcasts the service emits, and
        // this lets the rest of the integration suite keep using the real broadcaster for hub
        // tests that drive real SignalR clients.
        var scope = _factory.Services.CreateScope();
        _scopes.Add(scope);
        var repo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Urfu.Link.Services.Chat.Application.ChatEventDispatcher>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DisciplineConversationService>>();
        return new DisciplineConversationService(repo, _factory.ChatBroadcaster, dispatcher, logger);
    }

    private IConversationRepository ResolveRepo()
    {
        var scope = _factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IConversationRepository>();
    }

    private DisciplineEventConsumer ResolveConsumer()
    {
        if (_consumer is not null)
        {
            return _consumer;
        }

        _consumer = ActivatorUtilities.CreateInstance<DisciplineEventConsumer>(_factory.Services);
        return _consumer;
    }

    private readonly List<IServiceScope> _scopes = [];
    private DisciplineEventConsumer? _consumer;

    private static string BuildEnvelope<TEvent>(TEvent payload)
        where TEvent : IIntegrationEvent
    {
        var envelope = new IntegrationEnvelope<TEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: "discipline-service",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: payload);

        return JsonSerializer.Serialize(envelope, EnvelopeJsonOptions);
    }
}
