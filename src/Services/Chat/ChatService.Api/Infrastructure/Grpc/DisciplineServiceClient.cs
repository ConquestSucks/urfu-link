using System.Globalization;
using Grpc.Core;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Enums;
using DisciplineGrpc = Urfu.Link.Services.Disciplines.Grpc;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

/// <summary>
/// Production gRPC implementation of <see cref="IDisciplineServiceClient"/>. Wraps the
/// generated <see cref="DisciplineGrpc.InternalApi.InternalApiClient"/> so domain code stays
/// free of protobuf types.
/// </summary>
internal sealed class DisciplineServiceClient(
    DisciplineGrpc.InternalApi.InternalApiClient grpcClient,
    IGrpcBearerTokenProvider tokenProvider)
    : IDisciplineServiceClient
{
    public async Task<IReadOnlyList<UserDisciplineSnapshot>> ListUserDisciplinesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var request = new DisciplineGrpc.ListUserDisciplinesRequest
        {
            UserId = userId.ToString("D", CultureInfo.InvariantCulture),
        };

        var headers = await tokenProvider.GetAuthorizationMetadataAsync(cancellationToken).ConfigureAwait(false);
        var reply = await grpcClient
            .ListUserDisciplinesAsync(request, headers, cancellationToken: cancellationToken)
            .ResponseAsync
            .ConfigureAwait(false);

        return reply.Disciplines
            .Select(d => new UserDisciplineSnapshot(
                DisciplineId: Guid.Parse(d.DisciplineId),
                Code: d.Code,
                Title: d.Title,
                Role: MapRole(d.Role),
                SubgroupId: TryParseGuid(d.SubgroupId),
                VisibleSubgroupIds: d.VisibleSubgroupIds
                    .Select(TryParseGuid)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<DisciplineMember>> ListMembersAsync(
        Guid disciplineId,
        CancellationToken cancellationToken)
    {
        var request = new DisciplineGrpc.ListMembersRequest
        {
            DisciplineId = disciplineId.ToString("D", CultureInfo.InvariantCulture),
        };

        var headers = await tokenProvider.GetAuthorizationMetadataAsync(cancellationToken).ConfigureAwait(false);
        var reply = await grpcClient
            .ListMembersAsync(request, headers, cancellationToken: cancellationToken)
            .ResponseAsync
            .ConfigureAwait(false);

        if (!reply.Exists)
        {
            return [];
        }

        return reply.Members
            .Select(m => new DisciplineMember(
                UserId: Guid.Parse(m.UserId),
                Role: MapRole(m.Role),
                SubgroupId: TryParseGuid(m.SubgroupId)))
            .ToList();
    }

    public async Task<DisciplineSnapshotPage> ListDisciplineSnapshotsAsync(
        string? pageToken,
        int pageSize,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var request = new DisciplineGrpc.ListDisciplineSnapshotsRequest
        {
            IncludeArchived = includeArchived,
            PageSize = pageSize,
            PageToken = pageToken ?? string.Empty,
        };

        var headers = await tokenProvider.GetAuthorizationMetadataAsync(cancellationToken).ConfigureAwait(false);
        var reply = await grpcClient
            .ListDisciplineSnapshotsAsync(request, headers, cancellationToken: cancellationToken)
            .ResponseAsync
            .ConfigureAwait(false);

        return new DisciplineSnapshotPage(
            reply.Disciplines.Select(MapSnapshot).ToList(),
            string.IsNullOrWhiteSpace(reply.NextPageToken) ? null : reply.NextPageToken);
    }

    private static DisciplineSnapshot MapSnapshot(DisciplineGrpc.DisciplineSnapshotInfo snapshot)
    {
        return new DisciplineSnapshot(
            DisciplineId: Guid.Parse(snapshot.DisciplineId),
            Code: snapshot.Code,
            Title: snapshot.Title,
            Semester: snapshot.Semester,
            OwnerTeacherId: Guid.Parse(snapshot.OwnerTeacherId),
            CoverAssetId: TryParseGuid(snapshot.CoverAssetId),
            CreatedAtUtc: ParseTimestamp(snapshot.CreatedAtUtc),
            UpdatedAtUtc: ParseTimestamp(snapshot.UpdatedAtUtc),
            ArchivedAtUtc: ParseNullableTimestamp(snapshot.ArchivedAtUtc),
            Subgroups: snapshot.Subgroups.Select(s => new DisciplineSubgroupSnapshot(
                    SubgroupId: Guid.Parse(s.SubgroupId),
                    Name: s.Name,
                    CreatedAtUtc: ParseTimestamp(s.CreatedAtUtc),
                    UpdatedAtUtc: ParseTimestamp(s.UpdatedAtUtc),
                    ArchivedAtUtc: ParseNullableTimestamp(s.ArchivedAtUtc)))
                .ToList(),
            Members: snapshot.Members.Select(m => new DisciplineMember(
                    UserId: Guid.Parse(m.UserId),
                    Role: MapRole(m.Role),
                    SubgroupId: TryParseGuid(m.SubgroupId)))
                .ToList());
    }

    private static ParticipantRole MapRole(DisciplineGrpc.MembershipRole role) => role switch
    {
        DisciplineGrpc.MembershipRole.Teacher => ParticipantRole.Teacher,
        DisciplineGrpc.MembershipRole.Student => ParticipantRole.Student,
        _ => ParticipantRole.Member,
    };

    private static Guid? TryParseGuid(string? raw)
        => Guid.TryParse(raw, out var id) ? id : null;

    private static DateTimeOffset ParseTimestamp(string raw)
        => DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTimeOffset? ParseNullableTimestamp(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? null
            : DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
