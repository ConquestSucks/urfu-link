namespace Urfu.Link.Services.Presence.Domain.Interfaces;

public interface ITypingStore
{
    /// <summary>Returns true if the user was not already marked as typing in this conversation.</summary>
    Task<bool> StartTypingAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns true if the user was actively typing.</summary>
    Task<bool> StopTypingAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);

    Task<bool> IsTypingAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);
}
