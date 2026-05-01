using FluentAssertions;
using Grpc.Core;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;
using DisciplineGrpc = Urfu.Link.Services.Disciplines.Grpc;

namespace Urfu.Link.Services.Chat.UnitTests.Infrastructure;

public class DisciplineServiceClientTests
{
    [Fact]
    public async Task ListUserDisciplinesAsync_MapsTeacherRoleToParticipantRoleTeacher()
    {
        var disciplineId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reply = new DisciplineGrpc.ListUserDisciplinesReply
        {
            Disciplines =
            {
                new DisciplineGrpc.UserDisciplineInfo
                {
                    DisciplineId = disciplineId.ToString("D"),
                    Code = "CS101",
                    Title = "Intro",
                    Role = DisciplineGrpc.MembershipRole.Teacher,
                },
            },
        };
        var grpcClient = new StubInternalApiClient(reply);
        var sut = new DisciplineServiceClient(grpcClient);

        var result = await sut.ListUserDisciplinesAsync(Guid.NewGuid(), default);

        var snapshot = result.Should().ContainSingle().Subject;
        snapshot.DisciplineId.Should().Be(disciplineId);
        snapshot.Code.Should().Be("CS101");
        snapshot.Title.Should().Be("Intro");
        snapshot.Role.Should().Be(ParticipantRole.Teacher);
    }

    [Fact]
    public async Task ListUserDisciplinesAsync_MapsStudentRoleToParticipantRoleStudent()
    {
        var reply = new DisciplineGrpc.ListUserDisciplinesReply
        {
            Disciplines =
            {
                new DisciplineGrpc.UserDisciplineInfo
                {
                    DisciplineId = Guid.NewGuid().ToString("D"),
                    Code = "X",
                    Title = "Y",
                    Role = DisciplineGrpc.MembershipRole.Student,
                },
            },
        };
        var sut = new DisciplineServiceClient(new StubInternalApiClient(reply));

        var result = await sut.ListUserDisciplinesAsync(Guid.NewGuid(), default);

        result.Should().ContainSingle().Which.Role.Should().Be(ParticipantRole.Student);
    }

    [Fact]
    public async Task ListUserDisciplinesAsync_MapsUnknownRoleToParticipantRoleMember()
    {
        var reply = new DisciplineGrpc.ListUserDisciplinesReply
        {
            Disciplines =
            {
                new DisciplineGrpc.UserDisciplineInfo
                {
                    DisciplineId = Guid.NewGuid().ToString("D"),
                    Code = "X",
                    Title = "Y",
                    Role = DisciplineGrpc.MembershipRole.Unknown,
                },
            },
        };
        var sut = new DisciplineServiceClient(new StubInternalApiClient(reply));

        var result = await sut.ListUserDisciplinesAsync(Guid.NewGuid(), default);

        result.Should().ContainSingle().Which.Role.Should().Be(ParticipantRole.Member);
    }

    [Fact]
    public async Task ListUserDisciplinesAsync_EmptyReply_ReturnsEmptyList()
    {
        var sut = new DisciplineServiceClient(
            new StubInternalApiClient(new DisciplineGrpc.ListUserDisciplinesReply()));

        var result = await sut.ListUserDisciplinesAsync(Guid.NewGuid(), default);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Subclasses the generated gRPC client and overrides the single method under test so the
    /// real <see cref="DisciplineServiceClient"/> can be exercised without any wire transport.
    /// </summary>
    private sealed class StubInternalApiClient(DisciplineGrpc.ListUserDisciplinesReply reply)
        : DisciplineGrpc.InternalApi.InternalApiClient
    {
        public override AsyncUnaryCall<DisciplineGrpc.ListUserDisciplinesReply> ListUserDisciplinesAsync(
            DisciplineGrpc.ListUserDisciplinesRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
            => new(
                Task.FromResult(reply),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
    }
}
