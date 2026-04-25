namespace Urfu.Link.Services.Chat.Infrastructure;

/// <summary>
/// Operator-tunable knobs for chat messenger features. Defaults match the issue spec
/// (#211) — 48h edit/delete TTLs, 5 pinned messages per conversation, 50 forwarded messages
/// per request, no emoji whitelist.
/// </summary>
public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    public int EditTtlHours { get; set; } = 48;

    public int DeleteForEveryoneTtlHours { get; set; } = 48;

    public int MaxPinnedMessages { get; set; } = 5;

    public int MaxForwardedMessages { get; set; } = 50;

    /// <summary>
    /// Whitelist of allowed reaction emojis. When empty (default), any non-empty emoji is
    /// accepted (subject to <see cref="MaxReactionEmojiLength"/>).
    /// </summary>
    public IReadOnlyList<string> AllowedReactionEmojis { get; set; } = Array.Empty<string>();

    public int MaxReactionEmojiLength { get; set; } = 16;

    public int MaxMentionsPerMessage { get; set; } = 50;

    public TimeSpan EditTtl => TimeSpan.FromHours(EditTtlHours);

    public TimeSpan DeleteForEveryoneTtl => TimeSpan.FromHours(DeleteForEveryoneTtlHours);
}
