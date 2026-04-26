namespace Urfu.Link.Services.Chat.Application.Conversations;

/// <summary>
/// Optional kind filter applied when listing the caller's conversations. <see cref="All"/>
/// returns every conversation the user is a participant of (default behaviour). The other
/// values let the UI segment the discipline group list from the direct chat list.
/// </summary>
public enum ConversationListFilter
{
    All = 0,
    Direct = 1,
    Discipline = 2,
}
