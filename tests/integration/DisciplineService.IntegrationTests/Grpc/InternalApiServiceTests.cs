using System.Net.Http.Json;
using DisciplineService.Api.Application.Contracts.Requests;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Grpc.Net.Client;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Disciplines.Grpc;

namespace DisciplineService.IntegrationTests.Grpc;

[Collection(IntegrationCollection.Name)]
public sealed class InternalApiServiceTests : IAsyncLifetime
{
    private readonly DisciplineServiceFactory _factory;

    public InternalApiServiceTests(DisciplineServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private InternalApi.InternalApiClient CreateClient()
    {
        // gRPC service is wired with .RequireAuthorization(); short-circuit the
        // JWT pipeline by seeding the test principal that TestAuthHandler will
        // surface for any incoming request.
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid(), "service");
        var handler = _factory.Server.CreateHandler();
        var channel = GrpcChannel.ForAddress(
            _factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = handler });
        return new InternalApi.InternalApiClient(channel);
    }

    [Fact]
    public async Task Ping_Returns_Pong()
    {
        var grpc = CreateClient();
        var reply = await grpc.PingAsync(new PingRequest { Message = "hi" });
        reply.Service.Should().Be("discipline-service");
        reply.Message.Should().Be("pong:hi");
    }

    [Fact]
    public async Task CheckMembership_OwnerTeacher_ReturnsTrue()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        var reply = await CreateClient().CheckMembershipAsync(new CheckMembershipRequest
        {
            DisciplineId = disc.Id.ToString("D"),
            UserId = teacherId.ToString("D"),
        });

        reply.IsMember.Should().BeTrue();
        reply.Role.Should().Be(MembershipRole.Teacher);
    }

    [Fact]
    public async Task CheckMembership_NonMember_ReturnsFalse()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        var reply = await CreateClient().CheckMembershipAsync(new CheckMembershipRequest
        {
            DisciplineId = disc.Id.ToString("D"),
            UserId = Guid.NewGuid().ToString("D"),
        });

        reply.IsMember.Should().BeFalse();
        reply.Role.Should().Be(MembershipRole.Unknown);
    }

    [Fact]
    public async Task ListMembers_ReturnsAllEnrollments()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);
        await EnrollAsync(disc.Id, teacherId, [(studentId, DisciplineRole.Student)]);

        var reply = await CreateClient().ListMembersAsync(new ListMembersRequest
        {
            DisciplineId = disc.Id.ToString("D"),
        });

        reply.Exists.Should().BeTrue();
        reply.Members.Should().HaveCount(2);
        reply.Members.Should().Contain(m => m.UserId == teacherId.ToString("D") && m.Role == MembershipRole.Teacher);
        reply.Members.Should().Contain(m => m.UserId == studentId.ToString("D") && m.Role == MembershipRole.Student);
    }

    [Fact]
    public async Task ListMembers_UnknownDiscipline_ReturnsExistsFalse()
    {
        var reply = await CreateClient().ListMembersAsync(new ListMembersRequest
        {
            DisciplineId = Guid.NewGuid().ToString("D"),
        });

        reply.Exists.Should().BeFalse();
        reply.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task ListUserDisciplines_ReturnsTeacherAndStudentMemberships()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disc1 = await CreateDisciplineAsync(teacherId);
        var disc2 = await CreateDisciplineAsync(Guid.NewGuid());
        await EnrollAsync(disc2.Id, disc2.OwnerTeacherId, [(studentId, DisciplineRole.Student)]);
        await EnrollAsync(disc1.Id, teacherId, [(studentId, DisciplineRole.Student)]);

        var reply = await CreateClient().ListUserDisciplinesAsync(new ListUserDisciplinesRequest
        {
            UserId = studentId.ToString("D"),
        });

        reply.Disciplines.Should().HaveCount(2);
        reply.Disciplines.Should().AllSatisfy(d => d.Role.Should().Be(MembershipRole.Student));
    }

    [Fact]
    public async Task GetDiscipline_ReturnsExistingMetadata()
    {
        var teacherId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);

        var reply = await CreateClient().GetDisciplineAsync(new GetDisciplineRequest
        {
            DisciplineId = disc.Id.ToString("D"),
        });

        reply.Exists.Should().BeTrue();
        reply.DisciplineId.Should().Be(disc.Id.ToString("D"));
        reply.OwnerTeacherId.Should().Be(teacherId.ToString("D"));
        reply.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task GetDiscipline_Unknown_ReturnsExistsFalse()
    {
        var reply = await CreateClient().GetDisciplineAsync(new GetDisciplineRequest
        {
            DisciplineId = Guid.NewGuid().ToString("D"),
        });
        reply.Exists.Should().BeFalse();
    }

    private async Task<DisciplineResponse> CreateDisciplineAsync(Guid teacherId)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Admin();
        var http = _factory.CreateClient();
        var resp = await http.PostAsJsonAsync(
            "/api/v1/disciplines",
            new CreateDisciplineRequest(
                $"CS-{Guid.NewGuid():N}".Substring(0, 12),
                "Intro",
                null,
                "2026-spring",
                teacherId,
                null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DisciplineResponse>())!;
    }

    private async Task EnrollAsync(
        Guid disciplineId,
        Guid teacherId,
        IReadOnlyList<(Guid UserId, DisciplineRole Role)> users)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Teacher(teacherId);
        var http = _factory.CreateClient();
        var resp = await http.PostAsJsonAsync(
            $"/api/v1/disciplines/{disciplineId}/enrollments",
            new
            {
                Enrollments = users.Select(u => new EnrollmentInput(u.UserId, u.Role)).ToList(),
            });
        resp.EnsureSuccessStatusCode();
    }
}
