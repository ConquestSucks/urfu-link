namespace Urfu.Link.Services.Chat.Domain.Enums;

/// <summary>
/// Role of a participant inside a conversation. <see cref="Member"/> is the default for
/// direct chats and any other one-on-one or unspecified case. <see cref="Teacher"/> /
/// <see cref="Student"/> are populated for group conversations created from disciplines.
/// </summary>
public enum ParticipantRole
{
    Member = 0,
    Teacher = 1,
    Student = 2,
}
