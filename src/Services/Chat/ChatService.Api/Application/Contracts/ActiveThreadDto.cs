using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Contracts;

/// <summary>
/// One entry in the user's "active threads" list. Surfaces the root message together with the
/// caller's subscription metadata so clients can render the list without follow-up lookups.
/// </summary>
public sealed record ActiveThreadDto(
    Guid RootMessageId,
    string ConversationId,
    MessageDto RootMessage,
    int ReplyCount,
    DateTimeOffset LastActivityAtUtc,
    ThreadSubscriptionReason Reason);
