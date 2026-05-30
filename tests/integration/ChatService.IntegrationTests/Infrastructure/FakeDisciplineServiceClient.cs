using Urfu.Link.Services.Chat.Application.Disciplines;

namespace ChatService.IntegrationTests.Infrastructure;

/// <summary>
/// Test stand-in for <see cref="IDisciplineServiceClient"/>. Uses an in-memory map keyed by
/// userId so tests can prime the disciplines a particular user is enrolled in without booting
/// the real DisciplineService.
/// </summary>
public sealed class FakeDisciplineServiceClient : IDisciplineServiceClient
{
    private readonly Dictionary<Guid, List<UserDisciplineSnapshot>> _disciplinesByUser = new();
    private readonly Dictionary<Guid, List<DisciplineMember>> _membersByDiscipline = new();
    private readonly List<DisciplineSnapshot> _disciplineSnapshots = new();
    private readonly System.Collections.Concurrent.ConcurrentBag<Guid> _calledForUsers = new();
    private readonly System.Collections.Concurrent.ConcurrentBag<Guid> _calledForDisciplines = new();

    public IReadOnlyCollection<Guid> CalledForUsers => _calledForUsers;

    public IReadOnlyCollection<Guid> CalledForDisciplines => _calledForDisciplines;

    public void Seed(Guid userId, params UserDisciplineSnapshot[] disciplines)
    {
        ArgumentNullException.ThrowIfNull(disciplines);
        _disciplinesByUser[userId] = disciplines.ToList();
    }

    public void SeedMembers(Guid disciplineId, params DisciplineMember[] members)
    {
        ArgumentNullException.ThrowIfNull(members);
        _membersByDiscipline[disciplineId] = members.ToList();
    }

    public void SeedSnapshots(params DisciplineSnapshot[] snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        _disciplineSnapshots.Clear();
        _disciplineSnapshots.AddRange(snapshots);
    }

    public void Reset()
    {
        _disciplinesByUser.Clear();
        _membersByDiscipline.Clear();
        _disciplineSnapshots.Clear();
        _calledForUsers.Clear();
        _calledForDisciplines.Clear();
    }

    public Task<IReadOnlyList<UserDisciplineSnapshot>> ListUserDisciplinesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        _calledForUsers.Add(userId);
        if (_disciplinesByUser.TryGetValue(userId, out var list))
        {
            return Task.FromResult<IReadOnlyList<UserDisciplineSnapshot>>(list.ToList());
        }
        return Task.FromResult<IReadOnlyList<UserDisciplineSnapshot>>(Array.Empty<UserDisciplineSnapshot>());
    }

    public Task<IReadOnlyList<DisciplineMember>> ListMembersAsync(
        Guid disciplineId,
        CancellationToken cancellationToken)
    {
        _calledForDisciplines.Add(disciplineId);
        if (_membersByDiscipline.TryGetValue(disciplineId, out var members))
        {
            return Task.FromResult<IReadOnlyList<DisciplineMember>>(members.ToList());
        }
        return Task.FromResult<IReadOnlyList<DisciplineMember>>(Array.Empty<DisciplineMember>());
    }

    public Task<DisciplineSnapshotPage> ListDisciplineSnapshotsAsync(
        string? pageToken,
        int pageSize,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var offset = int.TryParse(pageToken, out var parsed) ? parsed : 0;
        var normalizedPageSize = pageSize <= 0 ? 100 : pageSize;
        var source = includeArchived
            ? _disciplineSnapshots
            : _disciplineSnapshots.Where(s => !s.IsArchived).ToList();
        var items = source
            .Skip(offset)
            .Take(normalizedPageSize)
            .ToList();
        var next = offset + normalizedPageSize < source.Count
            ? (offset + normalizedPageSize).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
        return Task.FromResult(new DisciplineSnapshotPage(items, next));
    }
}
