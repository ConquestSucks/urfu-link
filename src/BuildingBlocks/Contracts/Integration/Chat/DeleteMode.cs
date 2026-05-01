namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

/// <summary>
/// How a chat message was deleted. Lives in <c>BuildingBlocks/Contracts</c> because
/// <see cref="ChatMessageDeletedEvent"/> crosses the service boundary, and downstream
/// consumers (NotificationService, analytics, audit) need to deserialize the field
/// without taking a typed dependency on ChatService internals.
/// </summary>
public enum DeleteMode
{
    ForMe = 0,
    ForEveryone = 1,
}
