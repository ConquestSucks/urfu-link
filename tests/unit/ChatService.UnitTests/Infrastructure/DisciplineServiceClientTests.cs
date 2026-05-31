using FluentAssertions;
using Grpc.Core;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;
using DisciplineGrpc = Urfu.Link.Services.Disciplines.Grpc;

namespace Urfu.Link.Services.Chat.UnitTests.Infrastructure;

public class DisciplineServiceClientTests
{
    [Fact]
    public async Task ListUserDisciplinesAsync_AddsAuthorizationMetadata_WhenBearerTokenIsAvailable()
    {
        var reply = new DisciplineGrpc.ListUserDisciplinesReply();
        var grpcClient = new StubInternalApiClient(reply);
        var sut = new DisciplineServiceClient(
            grpcClient,
            new StubGrpcBearerTokenProvider("service-token"));

        await sut.ListUserDisciplinesAsync(Guid.NewGuid(), default);

        grpcClient.LastHeaders.Should().NotBeNull();
        grpcClient.LastHeaders!.Should()
            .ContainSingle(h => h.Key == "authorization" && h.Value == "Bearer service-token");
    }

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
        var sut = new DisciplineServiceClient(grpcClient, new StubGrpcBearerTokenProvider(null));

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
        var sut = new DisciplineServiceClient(new StubInternalApiClient(reply), new StubGrpcBearerTokenProvider(null));

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
        var sut = new DisciplineServiceClient(new StubInternalApiClient(reply), new StubGrpcBearerTokenProvider(null));

        var result = await sut.ListUserDisciplinesAsync(Guid.NewGuid(), default);

        result.Should().ContainSingle().Which.Role.Should().Be(ParticipantRole.Member);
    }

    [Fact]
    public async Task ListUserDisciplinesAsync_EmptyReply_ReturnsEmptyList()
    {
        var sut = new DisciplineServiceClient(
            new StubInternalApiClient(new DisciplineGrpc.ListUserDisciplinesReply()),
            new StubGrpcBearerTokenProvider(null));

        var result = await sut.ListUserDisciplinesAsync(Guid.NewGuid(), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListDisciplineSnapshotsAsync_MapsSubgroupsAndMemberSubgroups()
    {
        var disciplineId = Guid.NewGuid();
        var subgroupId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var reply = new DisciplineGrpc.ListDisciplineSnapshotsReply
        {
            Disciplines =
            {
                new DisciplineGrpc.DisciplineSnapshotInfo
                {
                    DisciplineId = disciplineId.ToString("D"),
                    Code = "CS101",
                    Title = "Intro",
                    Semester = "2026",
                    OwnerTeacherId = teacherId.ToString("D"),
                    CreatedAtUtc = now.ToString("O"),
                    UpdatedAtUtc = now.ToString("O"),
                    Subgroups =
                    {
                        new DisciplineGrpc.DisciplineSubgroupSnapshotInfo
                        {
                            SubgroupId = subgroupId.ToString("D"),
                            Name = "Group A",
                            CreatedAtUtc = now.ToString("O"),
                            UpdatedAtUtc = now.ToString("O"),
                        },
                    },
                    Members =
                    {
                        new DisciplineGrpc.MemberInfo
                        {
                            UserId = studentId.ToString("D"),
                            Role = DisciplineGrpc.MembershipRole.Student,
                            SubgroupId = subgroupId.ToString("D"),
                        },
                    },
                },
            },
            NextPageToken = "100",
        };
        var sut = new DisciplineServiceClient(
            new StubInternalApiClient(new DisciplineGrpc.ListUserDisciplinesReply(), reply),
            new StubGrpcBearerTokenProvider(null));

        var result = await sut.ListDisciplineSnapshotsAsync(null, 100, true, default);

        result.NextPageToken.Should().Be("100");
        var snapshot = result.Items.Should().ContainSingle().Subject;
        snapshot.DisciplineId.Should().Be(disciplineId);
        snapshot.OwnerTeacherId.Should().Be(teacherId);
        snapshot.Subgroups.Should().ContainSingle().Which.SubgroupId.Should().Be(subgroupId);
        snapshot.Members.Should().ContainSingle().Which.Should().Match<DisciplineMember>(m =>
            m.UserId == studentId
            && m.Role == ParticipantRole.Student
            && m.SubgroupId == subgroupId);
    }

    /// <summary>
    /// Subclasses the generated gRPC client and overrides the single method under test so the
    /// real <see cref="DisciplineServiceClient"/> can be exercised without any wire transport.
    /// </summary>
    private sealed class StubInternalApiClient(
        DisciplineGrpc.ListUserDisciplinesReply userDisciplinesReply,
        DisciplineGrpc.ListDisciplineSnapshotsReply? disciplineSnapshotsReply = null)
        : DisciplineGrpc.InternalApi.InternalApiClient
    {
        public Metadata? LastHeaders { get; private set; }

        public override AsyncUnaryCall<DisciplineGrpc.ListUserDisciplinesReply> ListUserDisciplinesAsync(
            DisciplineGrpc.ListUserDisciplinesRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            LastHeaders = headers;
            return new AsyncUnaryCall<DisciplineGrpc.ListUserDisciplinesReply>(
                    Task.FromResult(userDisciplinesReply),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });
        }

        public override AsyncUnaryCall<DisciplineGrpc.ListDisciplineSnapshotsReply> ListDisciplineSnapshotsAsync(
            DisciplineGrpc.ListDisciplineSnapshotsRequest request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            LastHeaders = headers;
            return new AsyncUnaryCall<DisciplineGrpc.ListDisciplineSnapshotsReply>(
                    Task.FromResult(disciplineSnapshotsReply ?? new DisciplineGrpc.ListDisciplineSnapshotsReply()),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });
        }
    }

    private sealed class StubGrpcBearerTokenProvider(string? token) : IGrpcBearerTokenProvider
    {
        public ValueTask<Metadata?> GetAuthorizationMetadataAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return ValueTask.FromResult<Metadata?>(null);
            }

            return ValueTask.FromResult<Metadata?>(new Metadata
            {
                { "authorization", $"Bearer {token}" },
            });
        }
    }
}
