using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

/// <summary>
/// Stores the conversation→discipline mapping for notification handlers that need
/// discipline context.
/// </summary>
public sealed class ChatDisciplineConversationCreatedHandler(IDisciplineConversationLookup lookup)
{
    public Task HandleAsync(ChatDisciplineConversationCreatedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return lookup.RegisterAsync(integrationEvent.ConversationId, integrationEvent.DisciplineId, cancellationToken);
    }
}
