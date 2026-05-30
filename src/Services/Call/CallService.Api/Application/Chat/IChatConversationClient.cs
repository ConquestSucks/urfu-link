namespace Urfu.Link.Services.Call.Application.Chat;

public sealed record CallConversationMetadata(
    bool Exists,
    string Type,
    IReadOnlyList<Guid> ParticipantIds);

public interface IChatConversationClient
{
    Task<CallConversationMetadata> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken);
}
