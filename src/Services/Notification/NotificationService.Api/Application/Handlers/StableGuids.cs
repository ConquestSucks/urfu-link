using System.Security.Cryptography;
using System.Text;

namespace Urfu.Link.Services.Notification.Application.Handlers;

internal static class StableGuids
{
    public static Guid From(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
