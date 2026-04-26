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

    public void Seed(Guid userId, params UserDisciplineSnapshot[] disciplines)
    {
        ArgumentNullException.ThrowIfNull(disciplines);
        _disciplinesByUser[userId] = disciplines.ToList();
    }

    public void Reset() => _disciplinesByUser.Clear();

    public Task<IReadOnlyList<UserDisciplineSnapshot>> ListUserDisciplinesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (_disciplinesByUser.TryGetValue(userId, out var list))
        {
            return Task.FromResult<IReadOnlyList<UserDisciplineSnapshot>>(list.ToList());
        }
        return Task.FromResult<IReadOnlyList<UserDisciplineSnapshot>>(Array.Empty<UserDisciplineSnapshot>());
    }
}
