using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

/// <summary>
/// Stores the conversation→discipline mapping so subsequent <c>chat.message.sent.v1</c>
/// events can be classified as <c>ChatMessageDiscipline</c>.
/// </summary>
public sealed class ChatDisciplineConversationCreatedHandler(IDisciplineConversationLookup lookup)
{
    public Task HandleAsync(ChatDisciplineConversationCreatedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return lookup.RegisterAsync(integrationEvent.ConversationId, integrationEvent.DisciplineId, cancellationToken);
    }
}
