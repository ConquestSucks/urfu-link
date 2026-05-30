using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Call.Application.Calls;
using Urfu.Link.Services.Call.Application.Contracts;
using Urfu.Link.Services.Call.Infrastructure.Auth;

namespace Urfu.Link.Services.Call.Realtime;

[Authorize]
public sealed class CallHub(CallSessionService calls) : Hub<ICallClient>
{
    public Task<CallSessionDto> Accept(Guid callId)
        => calls.AcceptAsync(callId, Context.User!.GetUserId(), Context.ConnectionAborted);

    public Task<CallSessionDto> Decline(Guid callId)
        => calls.DeclineAsync(callId, Context.User!.GetUserId(), Context.ConnectionAborted);

    public Task<CallSessionDto> Cancel(Guid callId)
        => calls.CancelAsync(callId, Context.User!.GetUserId(), Context.ConnectionAborted);

    public Task<CallSessionDto> Leave(Guid callId)
        => calls.LeaveAsync(callId, Context.User!.GetUserId(), Context.ConnectionAborted);
}
