using System.Net;
using System.Net.Http.Json;
using DisciplineChatE2ETests.Infrastructure;
using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace DisciplineChatE2ETests;

[Collection(DisciplineChatE2ECollection.Name)]
public sealed class DisciplineChatConversationE2ETests(DisciplineChatE2EFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
        => await fixture.ResetAsync();

    public Task DisposeAsync()
    {
        TestAuthHandler.CurrentPrincipal = null;
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Reconcile_CreatesGeneralAndSubgroupChats_FromDisciplineHttpFlow()
    {
        var disciplineClient = fixture.Discipline.CreateClient();
        var chatClient = fixture.Chat.CreateClient();
        var adminId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin(adminId);
        var discipline = await CreateDisciplineAsync(disciplineClient, teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var subgroup = await CreateSubgroupAsync(disciplineClient, discipline.Id);
        await EnrollStudentAsync(disciplineClient, discipline.Id, subgroup.Id, studentId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin(adminId);
        var reportResponse = await chatClient.PostAsJsonAsync(
            "/api/v1/chat/discipline-conversations/reconcile",
            new { PageSize = 50 },
            E2EJson.Options);

        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await reportResponse.Content.ReadFromE2EJsonAsync<DisciplineConversationReconciliationReport>();
        report.Should().NotBeNull();
        report!.DisciplinesScanned.Should().Be(1);
        report.SubgroupsScanned.Should().Be(2);
        report.ConversationsCreated.Should().Be(3);
        report.ParticipantsAdded.Should().BeGreaterThan(0);

        var generalConversationId = $"discipline:{discipline.Id:N}";
        var subgroupConversationId = $"discipline:{discipline.Id:N}:subgroup:{subgroup.Id:N}";

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var teacherPage = await chatClient.GetFromE2EJsonAsync<CursorPage<ConversationDto>>(
            "/api/v1/chat/conversations?type=discipline&limit=20");
        teacherPage.Should().NotBeNull();

        var teacherGeneral = teacherPage!.Items.Single(c => c.Id == generalConversationId);
        teacherGeneral.DisciplineChatKind.Should().Be(DisciplineChatKind.General);
        teacherGeneral.Participants.Should().Contain(teacherId);

        var teacherSubgroup = teacherPage.Items.Single(c => c.Id == subgroupConversationId);
        teacherSubgroup.DisciplineChatKind.Should().Be(DisciplineChatKind.Subgroup);
        teacherSubgroup.DisciplineSubgroupId.Should().Be(subgroup.Id);
        teacherSubgroup.DisciplineSubgroupName.Should().Be(subgroup.Name);
        teacherSubgroup.Participants.Should().BeEquivalentTo([teacherId, studentId]);
        teacherSubgroup.Capabilities.Should().NotBeNull();
        teacherSubgroup.Capabilities!.CanStartGroupCall.Should().BeTrue();
        teacherSubgroup.ParticipantRoles.Should().NotBeNull();
        teacherSubgroup.ParticipantRoles![teacherId].Should().Be(ParticipantRole.Teacher);
        teacherSubgroup.ParticipantRoles![studentId].Should().Be(ParticipantRole.Student);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Student(studentId);
        var studentPage = await chatClient.GetFromE2EJsonAsync<CursorPage<ConversationDto>>(
            "/api/v1/chat/conversations?type=discipline&limit=20");
        studentPage.Should().NotBeNull();

        studentPage!.Items.Select(c => c.Id)
            .Should()
            .BeEquivalentTo([generalConversationId, subgroupConversationId]);
        var studentSubgroup = studentPage.Items.Single(c => c.Id == subgroupConversationId);
        studentSubgroup.Capabilities.Should().NotBeNull();
        studentSubgroup.Capabilities!.CanStartGroupCall.Should().BeFalse();
    }

    private static async Task<DisciplineResponseE2E> CreateDisciplineAsync(HttpClient client, Guid teacherId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disciplines")
        {
            Content = JsonContent.Create(
                new
                {
                    Code = $"E2E-{Guid.NewGuid():N}"[..12],
                    Title = "E2E Discipline",
                    Description = "Discipline created by cross-service E2E test.",
                    Semester = "2026-spring",
                    OwnerTeacherId = teacherId,
                    CoverAssetId = (Guid?)null,
                },
                options: E2EJson.Options),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromE2EJsonAsync<DisciplineResponseE2E>();
        body.Should().NotBeNull();
        return body!;
    }

    private static async Task<DisciplineSubgroupResponseE2E> CreateSubgroupAsync(HttpClient client, Guid disciplineId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/disciplines/{disciplineId:D}/subgroups",
            new { Name = "Practice E2E" },
            E2EJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromE2EJsonAsync<DisciplineSubgroupResponseE2E>();
        body.Should().NotBeNull();
        return body!;
    }

    private static async Task EnrollStudentAsync(
        HttpClient client,
        Guid disciplineId,
        Guid subgroupId,
        Guid studentId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/disciplines/{disciplineId:D}/enrollments")
        {
            Content = JsonContent.Create(
                new
                {
                    Enrollments = new[]
                    {
                        new
                        {
                            UserId = studentId,
                            Role = DisciplineRole.Student,
                            SubgroupId = (Guid?)subgroupId,
                        },
                    },
                },
                options: E2EJson.Options),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record DisciplineResponseE2E(Guid Id);

    private sealed record DisciplineSubgroupResponseE2E(Guid Id, string Name);
}
