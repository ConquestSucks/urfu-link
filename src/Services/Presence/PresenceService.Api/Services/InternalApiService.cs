using Grpc.Core;
using Urfu.Link.Services.Presence.Grpc;

namespace Urfu.Link.Services.Presence.Services;

public sealed class InternalApiService : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        _ = context;
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "presence-service",
            Utc = DateTimeOffset.UtcNow.ToString("O"),
        });
    }
}
