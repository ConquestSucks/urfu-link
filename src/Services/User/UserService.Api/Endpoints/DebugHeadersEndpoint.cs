using FastEndpoints;

namespace UserService.Api.Endpoints;

public sealed class DebugHeadersEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/me/debug-headers");
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var headers = HttpContext.Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString());
        return HttpContext.Response.SendAsync(headers, cancellation: ct);
    }
}
