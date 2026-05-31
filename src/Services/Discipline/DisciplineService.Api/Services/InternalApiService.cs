using System.Globalization;
using System.Text;
using System.Text.Json;
using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Persistence;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Disciplines.Grpc;

namespace DisciplineService.Api.Services;

public sealed class InternalApiService(
    IDisciplineRepository disciplines,
    IEnrollmentRepository enrollments,
    DisciplineDbContext dbContext) : InternalApi.InternalApiBase
{
    private const int DefaultSnapshotPageSize = 100;
    private const int MaxSnapshotPageSize = 500;

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

    public override async Task<ListDisciplineSnapshotsReply> ListDisciplineSnapshots(
        ListDisciplineSnapshotsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var pageSize = NormalizePageSize(request.PageSize);
        var pageToken = ParsePageToken(request.PageToken);

        var query = dbContext.Disciplines
            .AsNoTracking()
            .AsSplitQuery()
            .Include(d => d.Enrollments)
            .Include(d => d.Subgroups)
            .AsQueryable();

        if (!request.IncludeArchived)
        {
            query = query.Where(d => d.ArchivedAtUtc == null);
        }

        if (pageToken is not null)
        {
            query = query.Where(d =>
                d.Semester.CompareTo(pageToken.Semester) > 0
                || (d.Semester == pageToken.Semester && d.Code.CompareTo(pageToken.Code) > 0)
                || (d.Semester == pageToken.Semester
                    && d.Code == pageToken.Code
                    && d.Id.CompareTo(pageToken.DisciplineId) > 0));
        }

        var fetched = await query
            .OrderBy(d => d.Semester)
            .ThenBy(d => d.Code)
            .ThenBy(d => d.Id)
            .Take(pageSize + 1)
            .ToListAsync(context.CancellationToken)
            .ConfigureAwait(false);

        var pageItems = fetched.Take(pageSize).ToList();
        var reply = new ListDisciplineSnapshotsReply();
        foreach (var discipline in pageItems)
        {
            reply.Disciplines.Add(ToSnapshotInfo(discipline));
        }

        if (fetched.Count > pageSize)
        {
            reply.NextPageToken = EncodePageToken(pageItems[^1]);
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

    private static DisciplineSnapshotInfo ToSnapshotInfo(Discipline discipline)
    {
        var info = new DisciplineSnapshotInfo
        {
            DisciplineId = discipline.Id.ToString("D"),
            Code = discipline.Code,
            Title = discipline.Title,
            Semester = discipline.Semester,
            OwnerTeacherId = discipline.OwnerTeacherId.ToString("D"),
            CoverAssetId = discipline.CoverAssetId?.ToString("D") ?? string.Empty,
            CreatedAtUtc = ToInvariantString(discipline.CreatedAtUtc),
            UpdatedAtUtc = ToInvariantString(discipline.UpdatedAtUtc),
            ArchivedAtUtc = discipline.ArchivedAtUtc.HasValue
                ? ToInvariantString(discipline.ArchivedAtUtc.Value)
                : string.Empty,
            IsArchived = discipline.IsArchived,
        };

        info.Subgroups.AddRange(
            discipline.Subgroups
                .OrderBy(s => s.ArchivedAtUtc.HasValue)
                .ThenBy(s => s.Name)
                .Select(s => new DisciplineSubgroupSnapshotInfo
                {
                    SubgroupId = s.Id.ToString("D"),
                    Name = s.Name,
                    CreatedAtUtc = ToInvariantString(s.CreatedAtUtc),
                    UpdatedAtUtc = ToInvariantString(s.UpdatedAtUtc),
                    ArchivedAtUtc = s.ArchivedAtUtc.HasValue
                        ? ToInvariantString(s.ArchivedAtUtc.Value)
                        : string.Empty,
                    IsArchived = s.IsArchived,
                }));

        info.Members.AddRange(
            discipline.Enrollments
                .OrderBy(e => e.Role)
                .ThenBy(e => e.EnrolledAtUtc)
                .Select(e => new MemberInfo
                {
                    UserId = e.UserId.ToString("D"),
                    Role = ToProto(e.Role),
                    EnrolledAtUtc = ToInvariantString(e.EnrolledAtUtc),
                    SubgroupId = e.SubgroupId?.ToString("D") ?? string.Empty,
                }));

        return info;
    }

    private static int NormalizePageSize(int pageSize) => pageSize switch
    {
        <= 0 => DefaultSnapshotPageSize,
        > MaxSnapshotPageSize => MaxSnapshotPageSize,
        _ => pageSize,
    };

    private static string ToInvariantString(DateTimeOffset value)
        => value.ToString("O", CultureInfo.InvariantCulture);

    private static SnapshotPageToken? ParsePageToken(string? pageToken)
    {
        if (string.IsNullOrWhiteSpace(pageToken))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(pageToken));
            var token = JsonSerializer.Deserialize<SnapshotPageToken>(json);
            if (token is null
                || string.IsNullOrWhiteSpace(token.Semester)
                || string.IsNullOrWhiteSpace(token.Code)
                || token.DisciplineId == Guid.Empty)
            {
                throw new FormatException("Snapshot page token is empty.");
            }

            return token;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Bad page_token."));
        }
    }

    private static string EncodePageToken(Discipline discipline)
    {
        var token = new SnapshotPageToken(discipline.Semester, discipline.Code, discipline.Id);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(token)));
    }

    private static MembershipRole ToProto(DisciplineRole role) => role switch
    {
        DisciplineRole.Teacher => MembershipRole.Teacher,
        DisciplineRole.Student => MembershipRole.Student,
        _ => MembershipRole.Unknown,
    };

    private sealed record SnapshotPageToken(string Semester, string Code, Guid DisciplineId);
}
