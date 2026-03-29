using FastEndpoints;
using UserService.Api.Domain;

namespace UserService.Api.Endpoints;

public sealed class ServiceInfoEndpoint(ServiceProfile descriptor)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/");
        AllowAnonymous();
        Options(x => x.WithGroupName("root"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return HttpContext.Response.SendAsync(new
        {
            service = descriptor.ServiceName,
            datastore = descriptor.Datastore,
            utc = DateTimeOffset.UtcNow,
        }, cancellation: ct);
    }
}
