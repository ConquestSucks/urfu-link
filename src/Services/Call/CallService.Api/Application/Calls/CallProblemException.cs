namespace Urfu.Link.Services.Call.Application.Calls;

public sealed class CallProblemException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
