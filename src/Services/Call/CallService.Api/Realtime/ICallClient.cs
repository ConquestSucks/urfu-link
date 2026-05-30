using Urfu.Link.Services.Call.Application.Contracts;

namespace Urfu.Link.Services.Call.Realtime;

public interface ICallClient
{
    Task IncomingCall(CallSessionDto call);

    Task CallAccepted(CallSessionDto call);

    Task CallDeclined(CallSessionDto call);

    Task CallCancelled(CallSessionDto call);

    Task CallParticipantJoined(Guid callId, Guid userId);

    Task CallParticipantLeft(Guid callId, Guid userId);

    Task CallEnded(CallSessionDto call);
}
