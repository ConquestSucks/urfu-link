using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record SendMessageRequest(
    string ConversationId,
    Guid SenderId,
    string Body,
    IReadOnlyList<Attachment> Attachments,
    string ClientMessageId);
