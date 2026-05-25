namespace Urfu.Link.Services.Notification.Domain.Aggregates;

/// <summary>
/// Read model that maps a chat conversation id (string) to its parent discipline.
/// Populated from <c>chat.discipline_conversation_created.v1</c> events for handlers
/// that need discipline context.
/// </summary>
public sealed class DisciplineConversation
{
    public string ConversationId { get; private set; } = null!;

    public Guid DisciplineId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private DisciplineConversation()
    {
    }

    public static DisciplineConversation Create(string conversationId, Guid disciplineId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        if (disciplineId == Guid.Empty)
        {
            throw new ArgumentException("Discipline id is required.", nameof(disciplineId));
        }

        return new DisciplineConversation
        {
            ConversationId = conversationId,
            DisciplineId = disciplineId,
            CreatedAtUtc = nowUtc,
        };
    }
}
