using Grpc.Core;
using MediaService.Api.Grpc;

namespace MediaService.Api.Services;

public sealed class InternalApiService : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        _ = context;
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "media-service",
            Utc = DateTimeOffset.UtcNow.ToString("O"),
        });
    }
}
