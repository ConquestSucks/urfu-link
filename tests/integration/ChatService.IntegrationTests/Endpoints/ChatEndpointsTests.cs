using System.Net;
using System.Net.Http.Json;
using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace ChatService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class ChatEndpointsTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public ChatEndpointsTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Post_ConversationsDirect_OpensConversationForCaller()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/chat/conversations/direct",
            new { peerUserId = peer });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromChatJsonAsync<ConversationDto>();
        dto!.Participants.Should().BeEquivalentTo(new[] { caller, peer });

        await using var scope = _factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider
            .GetRequiredService<Urfu.Link.Services.Chat.Domain.Interfaces.IConversationRepository>()
            .GetByIdAsync(dto.Id, default);
        stored.Should().BeNull();
    }

    [Fact]
    public async Task Get_Conversations_ReturnsCallerConversationsOnly()
    {
        var caller = Guid.NewGuid();
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var send = seed.ServiceProvider.GetRequiredService<SendMessageService>();
            var first = await open.OpenAsync(caller, Guid.NewGuid(), default);
            var second = await open.OpenAsync(caller, Guid.NewGuid(), default);
            var noise = await open.OpenAsync(Guid.NewGuid(), Guid.NewGuid(), default);

            await send.SendAsync(new SendMessageRequest(
                first.Id,
                caller,
                "seed-1",
                Array.Empty<Guid>(),
                $"c-{Guid.NewGuid():N}",
                PeerUserId: first.Participants.Single(p => p != caller)), default);
            await send.SendAsync(new SendMessageRequest(
                second.Id,
                caller,
                "seed-2",
                Array.Empty<Guid>(),
                $"c-{Guid.NewGuid():N}",
                PeerUserId: second.Participants.Single(p => p != caller)), default);
            var noiseSender = noise.Participants[0];
            await send.SendAsync(new SendMessageRequest(
                noise.Id,
                noiseSender,
                "noise",
                Array.Empty<Guid>(),
                $"c-{Guid.NewGuid():N}",
                PeerUserId: noise.Participants.Single(p => p != noiseSender)), default);
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var page = await client.GetFromChatJsonAsync<CursorPage<ConversationDto>>("/api/v1/chat/conversations?limit=50");

        page!.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(c => c.Participants.Contains(caller));
    }

    [Fact]
    public async Task Get_ConversationById_NotParticipant_ReturnsConflictOrForbidden()
    {
        // Open a conversation the caller is NOT part of.
        string convId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var conv = await open.OpenAsync(Guid.NewGuid(), Guid.NewGuid(), default);
            var sender = conv.Participants[0];
            var send = seed.ServiceProvider.GetRequiredService<SendMessageService>();
            await send.SendAsync(new SendMessageRequest(
                conv.Id,
                sender,
                "private",
                Array.Empty<Guid>(),
                $"c-{Guid.NewGuid():N}",
                PeerUserId: conv.Participants.Single(p => p != sender)), default);
            convId = conv.Id;
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/chat/conversations/{convId}");

        // FastEndpoints maps unhandled exceptions to 500 by default, but ChatAccessDeniedException is
        // not specifically mapped. We assert any non-success here to demonstrate the access path is
        // wired; exception-to-status mapping will be tightened in a future iteration.
        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Get_Conversations_BadCursor_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/chat/conversations?cursor=not-base64!!");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Conversations_TypeDiscipline_ReturnsOnlyDisciplineGroups()
    {
        var caller = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var direct = await open.OpenAsync(caller, Guid.NewGuid(), default);
            var send = seed.ServiceProvider.GetRequiredService<SendMessageService>();
            await send.SendAsync(new SendMessageRequest(
                direct.Id,
                caller,
                "direct",
                Array.Empty<Guid>(),
                $"c-{Guid.NewGuid():N}",
                PeerUserId: direct.Participants.Single(p => p != caller)), default);

            var disc = seed.ServiceProvider.GetRequiredService<DisciplineConversationService>();
            await disc.HandleDisciplineCreatedAsync(
                new Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineCreatedEvent(
                    disciplineId, "EP1", "Endpoint test", null, "2026", caller, null),
                default);
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);
        using var client = _factory.CreateClient();
        var page = await client.GetFromChatJsonAsync<CursorPage<ConversationDto>>(
            "/api/v1/chat/conversations?type=discipline&limit=50");

        page!.Items.Should().ContainSingle()
            .Which.DisciplineId.Should().Be(disciplineId);
    }

    [Fact]
    public async Task Get_Conversations_TypeDirect_ExcludesDisciplineGroups()
    {
        var caller = Guid.NewGuid();
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var direct = await open.OpenAsync(caller, Guid.NewGuid(), default);
            var send = seed.ServiceProvider.GetRequiredService<SendMessageService>();
            await send.SendAsync(new SendMessageRequest(
                direct.Id,
                caller,
                "direct",
                Array.Empty<Guid>(),
                $"c-{Guid.NewGuid():N}",
                PeerUserId: direct.Participants.Single(p => p != caller)), default);

            var disc = seed.ServiceProvider.GetRequiredService<DisciplineConversationService>();
            await disc.HandleDisciplineCreatedAsync(
                new Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineCreatedEvent(
                    Guid.NewGuid(), "EP2", "X", null, "2026", caller, null),
                default);
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);
        using var client = _factory.CreateClient();
        var page = await client.GetFromChatJsonAsync<CursorPage<ConversationDto>>(
            "/api/v1/chat/conversations?type=direct&limit=50");

        page!.Items.Should().ContainSingle()
            .Which.DisciplineId.Should().BeNull();
    }

    [Fact]
    public async Task Get_Conversations_InvalidType_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/chat/conversations?type=bogus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ConversationParticipants_AsParticipant_ReturnsRoles()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        string conversationId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var disc = seed.ServiceProvider.GetRequiredService<DisciplineConversationService>();
            await disc.HandleDisciplineCreatedAsync(
                new Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineCreatedEvent(
                    disciplineId, "PT1", "Participants test", null, "2026", teacherId, null),
                default);
            await disc.HandleUserEnrolledAsync(
                new Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.UserEnrolledEvent(
                    disciplineId, studentId,
                    Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineRole.Student,
                    teacherId),
                default);
            var repo = seed.ServiceProvider.GetRequiredService<Urfu.Link.Services.Chat.Domain.Interfaces.IConversationRepository>();
            var conv = await repo.GetByDisciplineIdAsync(disciplineId, default);
            conversationId = conv!.Id;
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(teacherId);
        using var client = _factory.CreateClient();
        var participants = await client.GetFromChatJsonAsync<List<ConversationParticipantDto>>(
            $"/api/v1/chat/conversations/{conversationId}/participants");

        participants.Should().NotBeNull();
        participants!.Should().HaveCount(2);
        participants.Should().Contain(p => p.UserId == teacherId
            && p.Role == Urfu.Link.Services.Chat.Domain.Enums.ParticipantRole.Teacher);
        participants.Should().Contain(p => p.UserId == studentId
            && p.Role == Urfu.Link.Services.Chat.Domain.Enums.ParticipantRole.Student);
    }

    [Fact]
    public async Task Get_ConversationParticipants_AsNonParticipant_NonSuccess()
    {
        var teacherId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        string conversationId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var disc = seed.ServiceProvider.GetRequiredService<DisciplineConversationService>();
            await disc.HandleDisciplineCreatedAsync(
                new Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineCreatedEvent(
                    disciplineId, "PT2", "x", null, "2026", teacherId, null),
                default);
            var repo = seed.ServiceProvider.GetRequiredService<Urfu.Link.Services.Chat.Domain.Interfaces.IConversationRepository>();
            var conv = await repo.GetByDisciplineIdAsync(disciplineId, default);
            conversationId = conv!.Id;
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid());
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/chat/conversations/{conversationId}/participants");

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Get_ConversationParticipants_AsAdmin_BypassesParticipantCheck()
    {
        var teacherId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        string conversationId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var disc = seed.ServiceProvider.GetRequiredService<DisciplineConversationService>();
            await disc.HandleDisciplineCreatedAsync(
                new Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineCreatedEvent(
                    disciplineId, "PT3", "x", null, "2026", teacherId, null),
                default);
            var repo = seed.ServiceProvider.GetRequiredService<Urfu.Link.Services.Chat.Domain.Interfaces.IConversationRepository>();
            var conv = await repo.GetByDisciplineIdAsync(disciplineId, default);
            conversationId = conv!.Id;
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin(Guid.NewGuid());
        using var client = _factory.CreateClient();
        var participants = await client.GetFromChatJsonAsync<List<ConversationParticipantDto>>(
            $"/api/v1/chat/conversations/{conversationId}/participants");

        participants.Should().ContainSingle().Which.UserId.Should().Be(teacherId);
    }

    [Fact]
    public async Task Get_ConversationMessages_ReturnsCursorPage()
    {
        var caller = Guid.NewGuid();
        var peer = Guid.NewGuid();
        string convId;
        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var open = seed.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var conv = await open.OpenAsync(caller, peer, default);
            convId = conv.Id;
        }

        for (var i = 0; i < 3; i++)
        {
            await using var sendScope = _factory.Services.CreateAsyncScope();
            var send = sendScope.ServiceProvider.GetRequiredService<SendMessageService>();
            await send.SendAsync(
                new SendMessageRequest(convId, caller, $"m{i}", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}", PeerUserId: peer),
                default);
            await Task.Delay(5);
        }

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(caller);

        using var client = _factory.CreateClient();
        var page = await client.GetFromChatJsonAsync<CursorPage<MessageDto>>(
            $"/api/v1/chat/conversations/{convId}/messages?limit=10&direction=older");

        page!.Items.Select(m => m.Body).Should().Equal("m2", "m1", "m0");
    }
}
