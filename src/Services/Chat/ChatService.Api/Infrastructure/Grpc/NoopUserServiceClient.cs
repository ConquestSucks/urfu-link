using Urfu.Link.Services.Chat.Application.Users;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

// Возвращает пустой словарь — для тестов и on-prem без UserService gRPC.
// Conversation participants отдаются с пустым displayName/avatarUrl, фронт
// рендерит fallback "Пользователь {GUID short}".
internal sealed class NoopUserServiceClient : IUserServiceClient
{
    public Task<IReadOnlyDictionary<Guid, UserSummary>> BatchGetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyDictionary<Guid, UserSummary>>(
            new Dictionary<Guid, UserSummary>());
    }
}
