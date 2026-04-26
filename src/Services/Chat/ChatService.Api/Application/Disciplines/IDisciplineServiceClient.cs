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
    ParticipantRole Role);

/// <summary>
/// Inbound view of DisciplineService used by ChatService at SignalR connection time.
/// Production implementation is a thin wrapper around the gRPC InternalApi client; tests
/// substitute a fake without booting Kestrel.
/// </summary>
public interface IDisciplineServiceClient
{
    Task<IReadOnlyList<UserDisciplineSnapshot>> ListUserDisciplinesAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
