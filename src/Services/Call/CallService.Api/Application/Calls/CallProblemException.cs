using Microsoft.AspNetCore.Http;

namespace Urfu.Link.Services.Call.Application.Calls;

public sealed class CallProblemException : Exception
{
    private const int DefaultStatusCode = StatusCodes.Status500InternalServerError;

    public CallProblemException()
        : this(DefaultStatusCode, "A call error occurred.")
    {
    }

    public CallProblemException(string message)
        : this(DefaultStatusCode, message)
    {
    }

    public CallProblemException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = DefaultStatusCode;
    }

    public CallProblemException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
