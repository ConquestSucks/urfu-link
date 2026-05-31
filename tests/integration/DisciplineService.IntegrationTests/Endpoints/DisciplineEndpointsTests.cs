using System.Net;
using System.Net.Http.Json;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Endpoints;
using DisciplineService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public sealed class DisciplineEndpointsTests : IAsyncLifetime
{
    private readonly DisciplineServiceFactory _factory;

    public DisciplineEndpointsTests(DisciplineServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateClient() => _factory.CreateClient();

    [Fact]
    public async Task Post_Disciplines_Returns405()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/disciplines",
            new
            {
                Code = "CS101",
                Title = "Intro",
                Semester = "2026-spring",
                OwnerTeacherId = Guid.NewGuid(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Get_DisciplineById_AsAdmin_ReturnsBody()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().GetAsync($"/api/v1/disciplines/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DisciplineResponse>();
        body!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Get_DisciplineById_NonMember_Returns403()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Student(Guid.NewGuid());
        var response = await CreateClient().GetAsync($"/api/v1/disciplines/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_DisciplineById_AsOwnerTeacher_ReturnsOk()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateClient().GetAsync($"/api/v1/disciplines/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_DisciplineById_NotFound_Returns404()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().GetAsync($"/api/v1/disciplines/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_Discipline_Returns405()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().PutAsJsonAsync(
            $"/api/v1/disciplines/{created.Id}",
            new
            {
                Code = created.Code,
                Title = "X",
                Description = (string?)null,
                Semester = "2026-spring",
                CoverAssetId = (Guid?)null,
            });
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Delete_Discipline_Returns405()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().DeleteAsync($"/api/v1/disciplines/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Mutate_Subgroups_IsNotAvailable()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);
        var subgroupId = created.Subgroups[0].Id;

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();

        var createResponse = await CreateClient().PostAsJsonAsync(
            $"/api/v1/disciplines/{created.Id}/subgroups",
            new { Name = "Practice" });
        var updateResponse = await CreateClient().PatchAsJsonAsync(
            $"/api/v1/disciplines/{created.Id}/subgroups/{subgroupId}",
            new { Name = "Renamed" });
        var deleteResponse = await CreateClient().DeleteAsync(
            $"/api/v1/disciplines/{created.Id}/subgroups/{subgroupId}");

        createResponse.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mutate_Enrollments_IsNotAvailable()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);
        var subgroupId = created.Subgroups[0].Id;

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();

        var enrollResponse = await CreateClient().PostAsJsonAsync(
            $"/api/v1/disciplines/{created.Id}/enrollments",
            new
            {
                Enrollments = new[]
                {
                    new { UserId = studentId, Role = DisciplineRole.Student, SubgroupId = (Guid?)subgroupId },
                },
            });
        var roleResponse = await CreateClient().PatchAsJsonAsync(
            $"/api/v1/disciplines/{created.Id}/enrollments/{studentId}/role",
            new { Role = DisciplineRole.Teacher });
        var subgroupResponse = await CreateClient().PatchAsJsonAsync(
            $"/api/v1/disciplines/{created.Id}/enrollments/{studentId}/subgroup",
            new { SubgroupId = subgroupId });
        var deleteResponse = await CreateClient().DeleteAsync(
            $"/api/v1/disciplines/{created.Id}/enrollments/{studentId}");

        enrollResponse.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        roleResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        subgroupResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Disciplines_AsAdmin_ListsAll()
    {
        await CreateDisciplineAsync(Guid.NewGuid());
        await CreateDisciplineAsync(Guid.NewGuid());

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().GetAsync("/api/v1/disciplines");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListDisciplinesResponse>();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Get_Disciplines_AsTeacher_ScopesToOwnDiscipline()
    {
        var teacherId = Guid.NewGuid();
        var mine = await CreateDisciplineAsync(teacherId);
        await CreateDisciplineAsync(Guid.NewGuid());

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateClient().GetAsync("/api/v1/disciplines");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListDisciplinesResponse>();
        body!.Items.Should().ContainSingle(i => i.Id == mine.Id);
    }

    [Fact]
    public async Task Get_Enrollments_PaginatesViaCursor()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);
        var students = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        await EnrollAsync(disc.Id, teacherId, students.Select(s => (s, DisciplineRole.Student)).ToList());

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var firstResp = await CreateClient().GetAsync($"/api/v1/disciplines/{disc.Id}/enrollments?limit=2");
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await firstResp.Content.ReadFromJsonAsync<ListEnrollmentsResponse>();
        first!.Items.Should().HaveCount(2);
        first.NextCursor.Should().NotBeNullOrWhiteSpace();

        var secondResp = await CreateClient().GetAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments?limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        secondResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await secondResp.Content.ReadFromJsonAsync<ListEnrollmentsResponse>();
        second!.Items.Should().HaveCount(2);
        second.Items.Select(i => i.UserId).Should().NotIntersectWith(first.Items.Select(i => i.UserId));
    }

    [Fact]
    public async Task Get_Enrollments_NonMember_Returns403()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Student(Guid.NewGuid());
        var response = await CreateClient().GetAsync($"/api/v1/disciplines/{disc.Id}/enrollments");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Enrollments_BadCursor_Returns400()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().GetAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments?cursor=not-a-valid-cursor");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_DisciplinesMe_ListsMyMemberships()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);
        await EnrollAsync(disc.Id, teacherId, [(studentId, DisciplineRole.Student)]);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Student(studentId);
        var response = await CreateClient().GetAsync("/api/v1/disciplines/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListMyDisciplinesResponse>();
        body!.Items.Should().ContainSingle(i => i.Id == disc.Id && i.Role == DisciplineRole.Student);
    }

    private async Task<DisciplineResponse> CreateDisciplineAsync(Guid teacherId)
        => await _factory.SeedDisciplineAsync(teacherId);

    private async Task EnrollAsync(
        Guid disciplineId,
        Guid teacherId,
        IReadOnlyList<(Guid UserId, DisciplineRole Role)> users)
        => await _factory.SeedEnrollmentsAsync(disciplineId, teacherId, users);

}
