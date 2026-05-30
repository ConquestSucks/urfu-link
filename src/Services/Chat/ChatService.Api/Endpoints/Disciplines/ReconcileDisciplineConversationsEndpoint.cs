using FastEndpoints;
using Urfu.Link.Services.Chat.Endpoints;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Disciplines;

public sealed class ReconcileDisciplineConversationsRequest
{
    public int PageSize { get; init; } = 100;
}

public sealed class ReconcileDisciplineConversationsEndpoint(
    DisciplineConversationReconciliationService service)
    : Endpoint<ReconcileDisciplineConversationsRequest, DisciplineConversationReconciliationReport>
{
    public override void Configure()
    {
        Post("discipline-conversations/reconcile");
        Group<ChatGroup>();
        Summary(s => s.Summary = "Admin-only reconciliation of discipline-backed chat conversations.");
    }

    public override async Task HandleAsync(
        ReconcileDisciplineConversationsRequest req,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (!User.IsAdmin())
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        var report = await service.ReconcileAsync(req.PageSize, ct).ConfigureAwait(false);
        await Send.OkAsync(report, ct).ConfigureAwait(false);
    }
}
