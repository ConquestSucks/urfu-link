namespace UserService.Api.Domain.ValueObjects;

public sealed record NotificationSettings(
    IReadOnlyDictionary<int, ChannelToggle> Categories,
    QuietHours QuietHours,
    bool DndEnabled,
    string Locale,
    bool Sound,
    IReadOnlyList<string> MutedConversationIds)
{
    public const string DefaultLocale = "ru-RU";

    public static NotificationSettings Default { get; } = new(
        BuildDefaultCategories(),
        QuietHours.Default,
        DndEnabled: false,
        Locale: DefaultLocale,
        Sound: true,
        MutedConversationIds: Array.Empty<string>());

    public ChannelToggle GetToggle(int category)
        => Categories.TryGetValue(category, out var toggle) ? toggle : ChannelToggle.AllOn;

    public NotificationSettings WithCategory(int category, ChannelToggle toggle)
    {
        ArgumentNullException.ThrowIfNull(toggle);
        var copy = new Dictionary<int, ChannelToggle>(Categories) { [category] = toggle };
        return this with { Categories = copy };
    }

    public NotificationSettings WithQuietHours(QuietHours quietHours)
    {
        ArgumentNullException.ThrowIfNull(quietHours);
        return this with { QuietHours = quietHours };
    }

    public NotificationSettings WithDnd(bool enabled) => this with { DndEnabled = enabled };

    public NotificationSettings WithLocale(string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        return this with { Locale = locale.Trim() };
    }

    public NotificationSettings WithSound(bool enabled) => this with { Sound = enabled };

    public NotificationSettings WithMutedConversation(string conversationId)
    {
        var normalized = NormalizeConversationId(conversationId);
        if (IsConversationMuted(normalized))
        {
            return this;
        }

        return this with { MutedConversationIds = MutedConversationIds.Append(normalized).ToArray() };
    }

    public NotificationSettings WithoutMutedConversation(string conversationId)
    {
        var normalized = NormalizeConversationId(conversationId);
        if (!IsConversationMuted(normalized))
        {
            return this;
        }

        return this with
        {
            MutedConversationIds = MutedConversationIds
                .Where(id => !string.Equals(id, normalized, StringComparison.Ordinal))
                .ToArray(),
        };
    }

    public bool IsConversationMuted(string conversationId)
        => MutedConversationIds.Any(id => string.Equals(id, conversationId, StringComparison.Ordinal));

    public NotificationSettings WithLegacyToggles(
        bool newMessages,
        bool sound,
        bool disciplineChatMessages,
        bool mentions)
    {
        var copy = new Dictionary<int, ChannelToggle>(Categories)
        {
            [NotificationCategoryCode.ChatMessageDirect] = newMessages ? ChannelToggle.AllOn : ChannelToggle.AllOff,
            [NotificationCategoryCode.ChatMessageDiscipline] = disciplineChatMessages ? ChannelToggle.AllOn : ChannelToggle.AllOff,
            [NotificationCategoryCode.ChatMessageMention] = mentions ? ChannelToggle.AllOn : ChannelToggle.AllOff,
        };

        return this with { Categories = copy, Sound = sound };
    }

    public static NotificationSettings FromLegacy(
        bool newMessages,
        bool sound,
        bool disciplineChatMessages,
        bool mentions)
        => Default.WithLegacyToggles(newMessages, sound, disciplineChatMessages, mentions);

    private static Dictionary<int, ChannelToggle> BuildDefaultCategories()
    {
        var dict = new Dictionary<int, ChannelToggle>(NotificationCategoryCode.All.Count);
        foreach (var code in NotificationCategoryCode.All)
        {
            dict[code] = ChannelToggle.AllOn;
        }

        return dict;
    }

    private static string NormalizeConversationId(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return conversationId.Trim();
    }
}
