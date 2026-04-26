namespace Urfu.Link.Services.Notification.Domain.Interfaces;

public interface IDisciplineConversationLookup
{
    Task<bool> IsDisciplineConversationAsync(string conversationId, CancellationToken cancellationToken);

    Task RegisterAsync(string conversationId, Guid disciplineId, CancellationToken cancellationToken);
}
