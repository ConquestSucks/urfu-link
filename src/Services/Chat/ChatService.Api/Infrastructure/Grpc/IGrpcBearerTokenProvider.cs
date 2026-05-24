using Grpc.Core;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

internal interface IGrpcBearerTokenProvider
{
    ValueTask<Metadata?> GetAuthorizationMetadataAsync(CancellationToken cancellationToken);
}
