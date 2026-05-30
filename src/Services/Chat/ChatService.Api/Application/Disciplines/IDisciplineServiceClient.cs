using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Disciplines;

/// <summary>
/// Snapshot of a discipline a user is enrolled in. Returned by the gRPC client when ChatService
/// bootstraps a SignalR connection so the hub can join the user to every conversation group
/// without round-tripping through MongoDB.
/// </summary>
public sealed record UserDisciplineSnapshot(
    Guid DisciplineId,
    string Code,
    string Title,
    ParticipantRole Role,
    Guid? SubgroupId = null,
    IReadOnlyList<Guid>? VisibleSubgroupIds = null);

/// <summary>
/// One member of a discipline (teacher or student). Returned by
/// <see cref="IDisciplineServiceClient.ListMembersAsync"/> so the mention resolver can
/// expand <c>@teachers</c> / <c>@students</c> into concrete user ids.
/// </summary>
public sealed record DisciplineMember(Guid UserId, ParticipantRole Role, Guid? SubgroupId = null);

public sealed record DisciplineSubgroupSnapshot(
    Guid SubgroupId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record DisciplineSnapshot(
    Guid DisciplineId,
    string Code,
    string Title,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    IReadOnlyList<DisciplineSubgroupSnapshot> Subgroups,
    IReadOnlyList<DisciplineMember> Members)
{
    public bool IsArchived => ArchivedAtUtc.HasValue;
}

public sealed record DisciplineSnapshotPage(
    IReadOnlyList<DisciplineSnapshot> Items,
    string? NextPageToken);

/// <summary>
/// Inbound view of DisciplineService used by ChatService at SignalR connection time and
/// by the mention resolver. Production implementation is a thin wrapper around the gRPC
/// InternalApi client; tests substitute a fake without booting Kestrel.
/// </summary>
public interface IDisciplineServiceClient
{
    Task<IReadOnlyList<UserDisciplineSnapshot>> ListUserDisciplinesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists every member of a discipline together with their role. Used by the
    /// <c>@teachers</c> / <c>@students</c> mention resolver in discipline conversations.
    /// Returns an empty list if the discipline does not exist (the gRPC <c>exists</c>
    /// flag is collapsed into "no members").
    /// </summary>
    Task<IReadOnlyList<DisciplineMember>> ListMembersAsync(
        Guid disciplineId,
        CancellationToken cancellationToken);

    Task<DisciplineSnapshotPage> ListDisciplineSnapshotsAsync(
        string? pageToken,
        int pageSize,
        bool includeArchived,
        CancellationToken cancellationToken);
}
