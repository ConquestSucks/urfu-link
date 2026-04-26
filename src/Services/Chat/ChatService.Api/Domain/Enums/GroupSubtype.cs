namespace Urfu.Link.Services.Chat.Domain.Enums;

/// <summary>
/// Sub-classification of a <see cref="ConversationType.Group"/> conversation. <see cref="Discipline"/>
/// is materialised from a Discipline aggregate via the discipline integration topic. <see cref="Common"/>
/// is reserved for future user-managed groups that are not bound to a discipline; it is not produced
/// by any current code path but the value exists so the wire and persistence layers can already round-trip it.
/// </summary>
public enum GroupSubtype
{
    Discipline = 0,
    Common = 1,
}
