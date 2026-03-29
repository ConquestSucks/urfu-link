using FastEndpoints;
using Urfu.Link.BuildingBlocks.Idempotency;
using UserService.Api.Application;

namespace UserService.Api.Endpoints;

public sealed class PublishSampleEventEndpoint(SampleEventDispatcher dispatcher)
    : Endpoint<PublishSampleEventRequest>
{
    public override void Configure()
    {
        Post("/integration/publish");
        Options(x => x.AddEndpointFilter<IdempotencyEndpointFilter>());
        Summary(s => s.Summary = "Publish a sample integration event");
    }

    public override async Task HandleAsync(PublishSampleEventRequest req, CancellationToken ct)
    {
        var messageId = await dispatcher.PublishAsync(req, ct).ConfigureAwait(false);
        HttpContext.Response.StatusCode = StatusCodes.Status202Accepted;
        await HttpContext.Response.SendAsync(new { MessageId = messageId }, cancellation: ct).ConfigureAwait(false);
    }
}
