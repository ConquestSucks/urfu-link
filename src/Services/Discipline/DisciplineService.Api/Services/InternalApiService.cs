using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Domain.Interfaces;
using Grpc.Core;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Disciplines.Grpc;

namespace DisciplineService.Api.Services;

public sealed class InternalApiService(
    IDisciplineRepository disciplines,
    IEnrollmentRepository enrollments) : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = context;
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "discipline-service",
            Utc = DateTimeOffset.UtcNow.ToString("O"),
        });
    }

    public override async Task<CheckMembershipReply> CheckMembership(
        CheckMembershipRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var (disciplineId, userId) = ParseGuids(request.DisciplineId, request.UserId);
        var discipline = await disciplines.GetByIdAsync(disciplineId, context.CancellationToken).ConfigureAwait(false);
        if (discipline is null)
        {
            return new CheckMembershipReply { IsMember = false, Role = MembershipRole.Unknown };
        }

        var enrollment = discipline.Enrollments.FirstOrDefault(e => e.UserId == userId);
        if (enrollment is null)
        {
            return new CheckMembershipReply { IsMember = false, Role = MembershipRole.Unknown };
        }

        return new CheckMembershipReply
        {
            IsMember = true,
            Role = ToProto(enrollment.Role),
        };
    }

    public override async Task<ListMembersReply> ListMembers(
        ListMembersRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.DisciplineId, out var disciplineId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad discipline_id."));
        }

        var discipline = await disciplines.GetByIdAsync(disciplineId, context.CancellationToken).ConfigureAwait(false);
        var reply = new ListMembersReply { Exists = discipline is not null };
        if (discipline is null)
        {
            return reply;
        }

        foreach (var enrollment in discipline.Enrollments.OrderBy(e => e.Role).ThenBy(e => e.EnrolledAtUtc))
        {
            reply.Members.Add(new MemberInfo
            {
                UserId = enrollment.UserId.ToString("D"),
                Role = ToProto(enrollment.Role),
                EnrolledAtUtc = enrollment.EnrolledAtUtc.ToString("O"),
                SubgroupId = enrollment.SubgroupId?.ToString("D") ?? string.Empty,
            });
        }

        return reply;
    }

    public override async Task<ListUserDisciplinesReply> ListUserDisciplines(
        ListUserDisciplinesRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad user_id."));
        }

        var memberships = await enrollments
            .ListMembershipsAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        var reply = new ListUserDisciplinesReply();
        foreach (var m in memberships)
        {
            var info = new UserDisciplineInfo
            {
                DisciplineId = m.DisciplineId.ToString("D"),
                Code = m.Code,
                Title = m.Title,
                Role = ToProto(m.Role),
                SubgroupId = m.SubgroupId?.ToString("D") ?? string.Empty,
            };

            var discipline = await disciplines.GetByIdAsync(m.DisciplineId, context.CancellationToken).ConfigureAwait(false);
            if (discipline is not null)
            {
                var visibleSubgroups = m.Role == DisciplineRole.Teacher
                    ? discipline.Subgroups.Where(s => !s.IsArchived).Select(s => s.Id)
                    : m.SubgroupId.HasValue
                        ? new[] { m.SubgroupId.Value }
                        : Enumerable.Empty<Guid>();
                info.VisibleSubgroupIds.AddRange(visibleSubgroups.Select(id => id.ToString("D")));
            }

            reply.Disciplines.Add(info);
        }

        return reply;
    }

    public override async Task<GetDisciplineReply> GetDiscipline(
        GetDisciplineRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.DisciplineId, out var disciplineId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad discipline_id."));
        }

        var discipline = await disciplines.GetByIdAsync(disciplineId, context.CancellationToken).ConfigureAwait(false);
        if (discipline is null)
        {
            return new GetDisciplineReply { Exists = false };
        }

        return new GetDisciplineReply
        {
            Exists = true,
            DisciplineId = discipline.Id.ToString("D"),
            Code = discipline.Code,
            Title = discipline.Title,
            OwnerTeacherId = discipline.OwnerTeacherId.ToString("D"),
            CoverAssetId = discipline.CoverAssetId?.ToString("D") ?? string.Empty,
            IsArchived = discipline.IsArchived,
        };
    }

    private static (Guid disciplineId, Guid userId) ParseGuids(string disciplineIdRaw, string userIdRaw)
    {
        if (!Guid.TryParse(disciplineIdRaw, out var disciplineId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad discipline_id."));
        }

        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad user_id."));
        }

        return (disciplineId, userId);
    }

    private static MembershipRole ToProto(DisciplineRole role) => role switch
    {
        DisciplineRole.Teacher => MembershipRole.Teacher,
        DisciplineRole.Student => MembershipRole.Student,
        _ => MembershipRole.Unknown,
    };
}
