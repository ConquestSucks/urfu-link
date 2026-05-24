using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Urfu.Link.Services.Chat.Application.Users;
using UserGrpc = Urfu.Link.Services.User.Grpc;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

internal sealed class UserServiceClient(
    UserGrpc.InternalApi.InternalApiClient grpcClient,
    ILogger<UserServiceClient> logger) : IUserServiceClient
{
    public async Task<IReadOnlyDictionary<Guid, UserSummary>> BatchGetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
            return new Dictionary<Guid, UserSummary>();

        var request = new UserGrpc.BatchGetUsersRequest();
        foreach (var id in userIds.Where(g => g != Guid.Empty).Distinct())
        {
            request.UserIds.Add(id.ToString("D", CultureInfo.InvariantCulture));
        }

        if (request.UserIds.Count == 0)
            return new Dictionary<Guid, UserSummary>();

        try
        {
            var reply = await grpcClient
                .BatchGetUsersAsync(request, cancellationToken: cancellationToken)
                .ResponseAsync
                .ConfigureAwait(false);

            var result = new Dictionary<Guid, UserSummary>(reply.Users.Count);
            foreach (var u in reply.Users)
            {
                if (!Guid.TryParse(u.UserId, out var parsed) || parsed == Guid.Empty)
                    continue;

                result[parsed] = new UserSummary(
                    UserId: parsed,
                    DisplayName: u.DisplayName ?? string.Empty,
                    AvatarUrl: u.AvatarUrl ?? string.Empty,
                    Email: u.Email ?? string.Empty);
            }
            return result;
        }
        catch (RpcException ex)
        {
            // Fail open: для @mentions / participants UI отсутствие имени — просто отсутствие
            // обогащения, а не критическая ошибка. Возвращаем пустой словарь, callers сами
            // подставят fallback (например, "Пользователь").
            logger.LogWarning(
                ex,
                "UserService BatchGetUsers failed for {Count} ids; returning empty.",
                userIds.Count);
            return new Dictionary<Guid, UserSummary>();
        }
    }
}
