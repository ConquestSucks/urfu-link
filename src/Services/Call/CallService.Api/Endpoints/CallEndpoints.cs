using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Urfu.Link.Services.Call.Application.Calls;
using Urfu.Link.Services.Call.Application.Contracts;
using Urfu.Link.Services.Call.Infrastructure.Auth;

namespace Urfu.Link.Services.Call.Endpoints;

public static class CallEndpoints
{
    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        group.MapPost("/conversations/{conversationId}", StartCallAsync);
        group.MapGet("/{callId:guid}", GetCallAsync);
        group.MapPost("/{callId:guid}/accept", AcceptCallAsync);
        group.MapPost("/{callId:guid}/decline", DeclineCallAsync);
        group.MapPost("/{callId:guid}/cancel", CancelCallAsync);
        group.MapPost("/{callId:guid}/leave", LeaveCallAsync);
        group.MapPost("/{callId:guid}/token", CreateTokenAsync);

        return app;
    }

    private static Task<Results<Ok<CallSessionDto>, ProblemHttpResult>> StartCallAsync(
        string conversationId,
        StartCallRequest request,
        CallSessionService calls,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => HandleAsync(
            () => calls.StartAsync(
                conversationId,
                httpContext.User.GetUserId(),
                request.CallType,
                cancellationToken));

    private static Task<Results<Ok<CallSessionDto>, ProblemHttpResult>> GetCallAsync(
        Guid callId,
        CallSessionService calls,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => HandleAsync(() => calls.GetAsync(callId, httpContext.User.GetUserId(), cancellationToken));

    private static Task<Results<Ok<CallSessionDto>, ProblemHttpResult>> AcceptCallAsync(
        Guid callId,
        CallSessionService calls,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => HandleAsync(() => calls.AcceptAsync(callId, httpContext.User.GetUserId(), cancellationToken));

    private static Task<Results<Ok<CallSessionDto>, ProblemHttpResult>> DeclineCallAsync(
        Guid callId,
        CallSessionService calls,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => HandleAsync(() => calls.DeclineAsync(callId, httpContext.User.GetUserId(), cancellationToken));

    private static Task<Results<Ok<CallSessionDto>, ProblemHttpResult>> CancelCallAsync(
        Guid callId,
        CallSessionService calls,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => HandleAsync(() => calls.CancelAsync(callId, httpContext.User.GetUserId(), cancellationToken));

    private static Task<Results<Ok<CallSessionDto>, ProblemHttpResult>> LeaveCallAsync(
        Guid callId,
        CallSessionService calls,
        HttpContext httpContext,
        CancellationToken cancellationToken)
        => HandleAsync(() => calls.LeaveAsync(callId, httpContext.User.GetUserId(), cancellationToken));

    private static async Task<Results<Ok<CallTokenDto>, ProblemHttpResult>> CreateTokenAsync(
        Guid callId,
        CallSessionService calls,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await calls.CreateTokenAsync(callId, httpContext.User.GetUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (CallProblemException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }

    private static async Task<Results<Ok<CallSessionDto>, ProblemHttpResult>> HandleAsync(
        Func<Task<CallSessionDto>> action)
    {
        try
        {
            return TypedResults.Ok(await action().ConfigureAwait(false));
        }
        catch (CallProblemException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: ex.StatusCode);
        }
    }
}
