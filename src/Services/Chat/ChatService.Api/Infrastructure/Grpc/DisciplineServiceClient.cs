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
                Role: MapRole(d.Role)))
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
                Role: MapRole(m.Role)))
            .ToList();
    }

    private static ParticipantRole MapRole(DisciplineGrpc.MembershipRole role) => role switch
    {
        DisciplineGrpc.MembershipRole.Teacher => ParticipantRole.Teacher,
        DisciplineGrpc.MembershipRole.Student => ParticipantRole.Student,
        _ => ParticipantRole.Member,
    };
}
