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
        // gRPC service requires the dedicated service-role policy. The test principal
        // mirrors the chat-service-internal Keycloak service-account: authenticated +
        // bearing the service:discipline-read realm role.
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(
            Guid.NewGuid(),
            "service:discipline-read");
        var handler = _factory.Server.CreateHandler();
        var channel = GrpcChannel.ForAddress(
            _factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = handler });
        return new InternalApi.InternalApiClient(channel);
    }

    private InternalApi.InternalApiClient CreateClientAs(params string[] roles)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(Guid.NewGuid(), roles);
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
    public async Task CallerWithoutDisciplineReadRole_GetsPermissionDenied()
    {
        // Authenticated end-user (student) has no service:discipline-read role and no admin
        // override, so the gRPC policy must reject before the handler executes — otherwise an
        // enrolled student could fetch any other discipline's roster.
        var grpc = CreateClientAs("student");
        var act = async () => await grpc.PingAsync(new PingRequest { Message = "denied" });

        var rpc = await act.Should().ThrowAsync<global::Grpc.Core.RpcException>();
        rpc.Which.StatusCode.Should().BeOneOf(
            global::Grpc.Core.StatusCode.PermissionDenied,
            global::Grpc.Core.StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task AdminCaller_Allowed()
    {
        var grpc = CreateClientAs("admin");
        var reply = await grpc.PingAsync(new PingRequest { Message = "admin" });
        reply.Service.Should().Be("discipline-service");
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
    public async Task ListDisciplineSnapshots_ReturnsSubgroupsAndEnrollmentSubgroupIds()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var disc = await CreateDisciplineAsync(teacherId);
        await EnrollAsync(disc.Id, teacherId, [(studentId, DisciplineRole.Student)]);

        var reply = await CreateClient().ListDisciplineSnapshotsAsync(new ListDisciplineSnapshotsRequest
        {
            IncludeArchived = true,
            PageSize = 10,
        });

        var snapshot = reply.Disciplines.Should().ContainSingle(d => d.DisciplineId == disc.Id.ToString("D")).Subject;
        snapshot.Subgroups.Should().ContainSingle(s => s.SubgroupId == disc.Subgroups[0].Id.ToString("D"));
        snapshot.Members.Should().Contain(m =>
            m.UserId == teacherId.ToString("D")
            && m.Role == MembershipRole.Teacher
            && string.IsNullOrEmpty(m.SubgroupId));
        snapshot.Members.Should().Contain(m =>
            m.UserId == studentId.ToString("D")
            && m.Role == MembershipRole.Student
            && m.SubgroupId == disc.Subgroups[0].Id.ToString("D"));
    }

    [Fact]
    public async Task ListDisciplineSnapshots_PaginatesWithKeysetToken()
    {
        var first = await CreateDisciplineAsync(Guid.NewGuid());
        var second = await CreateDisciplineAsync(Guid.NewGuid());

        var page1 = await CreateClient().ListDisciplineSnapshotsAsync(new ListDisciplineSnapshotsRequest
        {
            IncludeArchived = true,
            PageSize = 1,
        });
        page1.Disciplines.Should().ContainSingle();
        page1.NextPageToken.Should().NotBeNullOrWhiteSpace();

        var page2 = await CreateClient().ListDisciplineSnapshotsAsync(new ListDisciplineSnapshotsRequest
        {
            IncludeArchived = true,
            PageSize = 1,
            PageToken = page1.NextPageToken,
        });

        page2.Disciplines.Should().ContainSingle();
        page2.Disciplines[0].DisciplineId.Should().NotBe(page1.Disciplines[0].DisciplineId);
        var returnedIds = new[] { page1.Disciplines[0].DisciplineId, page2.Disciplines[0].DisciplineId };
        returnedIds.Should().BeEquivalentTo(new[] { first.Id.ToString("D"), second.Id.ToString("D") });
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
        => await _factory.SeedDisciplineAsync(teacherId, title: "Intro");

    private async Task EnrollAsync(
        Guid disciplineId,
        Guid teacherId,
        IReadOnlyList<(Guid UserId, DisciplineRole Role)> users)
        => await _factory.SeedEnrollmentsAsync(disciplineId, teacherId, users);
}
