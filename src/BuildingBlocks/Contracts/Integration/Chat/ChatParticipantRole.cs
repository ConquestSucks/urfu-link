namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

/// <summary>
/// Wire-format role of a chat conversation participant. Mirrors the chat-domain enum but is
/// kept as a separate type so the integration contract stays independent of internal domain
/// types — consumers in other services depend on this contract project, not on ChatService.
/// </summary>
public enum ChatParticipantRole
{
    Member = 0,
    Teacher = 1,
    Student = 2,
}
