using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record ForwardedFromDto(
    Guid OriginalSenderId,
    DateTimeOffset OriginalSentAtUtc,
    string? OriginalConversationId)
{
    public static ForwardedFromDto FromDomain(ForwardedFrom forwardedFrom)
    {
        ArgumentNullException.ThrowIfNull(forwardedFrom);
        return new ForwardedFromDto(
            forwardedFrom.OriginalSenderId,
            forwardedFrom.OriginalSentAtUtc,
            forwardedFrom.OriginalConversationId);
    }
}
