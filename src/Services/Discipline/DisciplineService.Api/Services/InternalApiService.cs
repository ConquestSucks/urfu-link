using Grpc.Core;
using Urfu.Link.Services.Disciplines.Grpc;

namespace DisciplineService.Api.Services;

public sealed class InternalApiService : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = context;
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "discipline-service",
            Utc = DateTimeOffset.UtcNow.ToString("O"),
        });
    }
}
