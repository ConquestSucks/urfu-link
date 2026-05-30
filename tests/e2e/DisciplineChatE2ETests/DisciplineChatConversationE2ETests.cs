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
    public async Task Reconcile_CreatesGeneralAndSubgroupChats_FromSeededDisciplineFlow()
    {
        var chatClient = fixture.Chat.CreateClient();
        var adminId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var discipline = await fixture.Discipline.SeedDisciplineAsync(teacherId);

        await fixture.Discipline.SeedEnrollmentAsync(
            discipline.Id,
            teacherId,
            studentId,
            DisciplineRole.Student,
            discipline.SubgroupId);

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
        var subgroupConversationId = $"discipline:{discipline.Id:N}:subgroup:{discipline.SubgroupId:N}";

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var teacherPage = await chatClient.GetFromE2EJsonAsync<CursorPage<ConversationDto>>(
            "/api/v1/chat/conversations?type=discipline&limit=20");
        teacherPage.Should().NotBeNull();

        var teacherGeneral = teacherPage!.Items.Single(c => c.Id == generalConversationId);
        teacherGeneral.DisciplineChatKind.Should().Be(DisciplineChatKind.General);
        teacherGeneral.Participants.Should().Contain(teacherId);

        var teacherSubgroup = teacherPage.Items.Single(c => c.Id == subgroupConversationId);
        teacherSubgroup.DisciplineChatKind.Should().Be(DisciplineChatKind.Subgroup);
        teacherSubgroup.DisciplineSubgroupId.Should().Be(discipline.SubgroupId);
        teacherSubgroup.DisciplineSubgroupName.Should().Be(discipline.SubgroupName);
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

}
