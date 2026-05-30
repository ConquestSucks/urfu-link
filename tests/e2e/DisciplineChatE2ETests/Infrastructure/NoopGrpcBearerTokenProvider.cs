using Grpc.Core;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;

namespace DisciplineChatE2ETests.Infrastructure;

internal sealed class NoopGrpcBearerTokenProvider : IGrpcBearerTokenProvider
{
    public ValueTask<Metadata?> GetAuthorizationMetadataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Metadata?>(null);
    }
}
