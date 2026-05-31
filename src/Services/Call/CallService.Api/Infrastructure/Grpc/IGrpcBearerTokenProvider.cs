using Grpc.Core;

namespace Urfu.Link.Services.Call.Infrastructure.Grpc;

internal interface IGrpcBearerTokenProvider
{
    ValueTask<Metadata?> GetAuthorizationMetadataAsync(CancellationToken cancellationToken);
}
