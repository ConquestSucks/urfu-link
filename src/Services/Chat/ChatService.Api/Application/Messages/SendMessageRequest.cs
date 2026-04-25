namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record SendMessageRequest(
    string ConversationId,
    Guid SenderId,
    string Body,
    IReadOnlyList<Guid> AttachmentAssetIds,
    string ClientMessageId);
