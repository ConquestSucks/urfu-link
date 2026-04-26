using System.Globalization;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Enums;
using DisciplineGrpc = Urfu.Link.Services.Disciplines.Grpc;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

/// <summary>
/// Production gRPC implementation of <see cref="IDisciplineServiceClient"/>. Wraps the
/// generated <see cref="DisciplineGrpc.InternalApi.InternalApiClient"/> so domain code stays
/// free of protobuf types.
/// </summary>
internal sealed class DisciplineServiceClient(DisciplineGrpc.InternalApi.InternalApiClient grpcClient)
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

        var reply = await grpcClient
            .ListUserDisciplinesAsync(request, cancellationToken: cancellationToken)
            .ResponseAsync
            .ConfigureAwait(false);

        return reply.Disciplines
            .Select(d => new UserDisciplineSnapshot(
                DisciplineId: Guid.Parse(d.DisciplineId),
                Code: d.Code,
                Title: d.Title,
                Role: MapRole(d.Role)))
            .ToList();
    }

    private static ParticipantRole MapRole(DisciplineGrpc.MembershipRole role) => role switch
    {
        DisciplineGrpc.MembershipRole.Teacher => ParticipantRole.Teacher,
        DisciplineGrpc.MembershipRole.Student => ParticipantRole.Student,
        _ => ParticipantRole.Member,
    };
}
