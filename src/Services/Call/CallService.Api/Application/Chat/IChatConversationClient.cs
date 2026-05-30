namespace Urfu.Link.Services.Call.Application.Chat;

public sealed record CallConversationMetadata(
    bool Exists,
    string Type,
    IReadOnlyList<Guid> ParticipantIds,
    string? GroupSubtype = null,
    string? DisciplineChatKind = null,
    IReadOnlyDictionary<Guid, string>? ParticipantRoles = null)
{
    public string RoleOf(Guid userId)
        => ParticipantRoles is not null && ParticipantRoles.TryGetValue(userId, out var role)
            ? role
            : "Member";
}

public interface IChatConversationClient
{
    Task<CallConversationMetadata> GetConversationAsync(
        string conversationId,
        CancellationToken cancellationToken);
}
