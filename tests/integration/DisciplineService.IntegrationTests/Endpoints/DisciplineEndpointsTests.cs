using System.Net;
using System.Net.Http.Json;
using DisciplineService.Api.Application.Contracts.Requests;
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

    /// <summary>
    /// Returns an HttpClient with a fresh <c>Idempotency-Key</c> header pre-attached.
    /// CreateDiscipline and EnrollUsers reject requests without the header so every
    /// mutating call needs a unique key — using a single static key would make the
    /// second call collide on the dedup window.
    /// </summary>
    private HttpClient CreateIdempotentClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return client;
    }

    private static CreateDisciplineRequest SampleCreate(Guid ownerId)
        => new(
            Code: $"CS-{Guid.NewGuid():N}".Substring(0, 12),
            Title: "Intro to CS",
            Description: "Foundational course",
            Semester: "2026-spring",
            OwnerTeacherId: ownerId,
            CoverAssetId: null);

    [Fact]
    public async Task Post_Disciplines_AsAdmin_Returns201_WithBodyAndPublishesEvents()
    {
        var teacherId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();

        var client = CreateIdempotentClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/disciplines",
            SampleCreate(teacherId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<DisciplineResponse>();
        body.Should().NotBeNull();
        body!.OwnerTeacherId.Should().Be(teacherId);
        body.Enrollments.Should().ContainSingle()
            .Which.Role.Should().Be(DisciplineRole.Teacher);

        _factory.OutboxWriter.Published.Should().Contain(p => p.EventType == "discipline.created.v1");
        _factory.OutboxWriter.Published.Should().Contain(p => p.EventType == "discipline.user_enrolled.v1");
    }

    [Fact]
    public async Task Post_Disciplines_WithoutAuth_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var client = CreateIdempotentClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/disciplines",
            SampleCreate(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Disciplines_AsTeacher_Returns403()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(Guid.NewGuid());
        var client = CreateIdempotentClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/disciplines",
            SampleCreate(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Disciplines_DuplicateCode_Returns409()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var teacherId = Guid.NewGuid();
        var first = SampleCreate(teacherId);

        // Two distinct Idempotency-Keys: we need to surface the unique-code conflict,
        // not have the second request rejected as a duplicate request envelope.
        var firstResponse = await CreateIdempotentClient().PostAsJsonAsync("/api/v1/disciplines", first);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = first with { Title = "Other" };
        var secondResponse = await CreateIdempotentClient().PostAsJsonAsync("/api/v1/disciplines", dup);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_Disciplines_WithoutIdempotencyKey_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/disciplines",
            SampleCreate(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Disciplines_DuplicateIdempotencyKey_Returns409()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var key = Guid.NewGuid().ToString("N");

        var first = _factory.CreateClient();
        first.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var firstResponse = await first.PostAsJsonAsync(
            "/api/v1/disciplines",
            SampleCreate(Guid.NewGuid()));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = _factory.CreateClient();
        second.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var secondResponse = await second.PostAsJsonAsync(
            "/api/v1/disciplines",
            SampleCreate(Guid.NewGuid()));
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_Disciplines_BlankCode_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/disciplines",
            new CreateDisciplineRequest(
                Code: "",
                Title: "X",
                Description: null,
                Semester: "2026-spring",
                OwnerTeacherId: Guid.NewGuid(),
                CoverAssetId: null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    public async Task Put_Discipline_AsOwner_UpdatesAndReturns204()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);
        _factory.OutboxWriter.Clear();

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateClient().PutAsJsonAsync(
            $"/api/v1/disciplines/{created.Id}",
            new
            {
                Code = created.Code,
                Title = "Updated Title",
                Description = "New desc",
                Semester = "2026-fall",
                CoverAssetId = (Guid?)null,
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.OutboxWriter.Published.Should().Contain(p => p.EventType == "discipline.updated.v1");
    }

    [Fact]
    public async Task Put_Discipline_AsStudent_Returns403()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Student(Guid.NewGuid());
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
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Discipline_AsAdmin_ArchivesAndReturns204()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);
        _factory.OutboxWriter.Clear();

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().DeleteAsync($"/api/v1/disciplines/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.OutboxWriter.Published.Should().Contain(p => p.EventType == "discipline.deleted.v1");
    }

    [Fact]
    public async Task Delete_Discipline_AsTeacher_Returns403()
    {
        var teacherId = Guid.NewGuid();
        var created = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateClient().DeleteAsync($"/api/v1/disciplines/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    [Fact]
    public async Task Post_Enrollments_AsOwner_AddsParticipantsAndPublishesEvents()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);
        _factory.OutboxWriter.Clear();

        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateIdempotentClient().PostAsJsonAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments",
            new
            {
                Enrollments = new[]
                {
                    new EnrollmentInput(s1, DisciplineRole.Student),
                    new EnrollmentInput(s2, DisciplineRole.Student),
                },
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EnrollUsersResponse>();
        body!.Enrollments.Should().HaveCount(2);
        _factory.OutboxWriter.Published.Count(p => p.EventType == "discipline.user_enrolled.v1")
            .Should().Be(2);
    }

    [Fact]
    public async Task Post_Enrollments_DuplicateUser_Returns409()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateIdempotentClient().PostAsJsonAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments",
            new
            {
                Enrollments = new[] { new EnrollmentInput(teacherId, DisciplineRole.Teacher) },
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_Enrollment_AsOwner_RemovesParticipant()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);
        await EnrollAsync(disc.Id, teacherId, [(studentId, DisciplineRole.Student)]);
        _factory.OutboxWriter.Clear();

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateClient().DeleteAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments/{studentId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.OutboxWriter.Published.Should().Contain(p => p.EventType == "discipline.user_unenrolled.v1");
    }

    [Fact]
    public async Task Delete_Enrollment_OwnerTeacher_Returns409()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().DeleteAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments/{teacherId}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Patch_EnrollmentRole_AsOwner_ChangesRole()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);
        await EnrollAsync(disc.Id, teacherId, [(studentId, DisciplineRole.Student)]);
        _factory.OutboxWriter.Clear();

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var response = await CreateClient().PatchAsJsonAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments/{studentId}/role",
            new { Role = DisciplineRole.Teacher });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.OutboxWriter.Published.Should().Contain(p => p.EventType == "discipline.enrollment_role_changed.v1");
    }

    [Fact]
    public async Task Patch_EnrollmentRole_OnOwnerTeacher_Returns409()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var response = await CreateClient().PatchAsJsonAsync(
            $"/api/v1/disciplines/{disc.Id}/enrollments/{teacherId}/role",
            new { Role = DisciplineRole.Student });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<DisciplineResponse> CreateDisciplineAsync(Guid teacherId)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var resp = await CreateIdempotentClient().PostAsJsonAsync("/api/v1/disciplines", SampleCreate(teacherId));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DisciplineResponse>())!;
    }

    private async Task EnrollAsync(
        Guid disciplineId,
        Guid teacherId,
        IReadOnlyList<(Guid UserId, DisciplineRole Role)> users)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var resp = await CreateIdempotentClient().PostAsJsonAsync(
            $"/api/v1/disciplines/{disciplineId}/enrollments",
            new
            {
                Enrollments = users.Select(u => new EnrollmentInput(u.UserId, u.Role)).ToList(),
            });
        resp.EnsureSuccessStatusCode();
    }
}
