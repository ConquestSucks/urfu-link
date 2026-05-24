namespace UserService.Api.Domain.ValueObjects;

public sealed record NotificationSettings(
    IReadOnlyDictionary<int, ChannelToggle> Categories,
    QuietHours QuietHours,
    bool DndEnabled,
    string Locale,
    bool Sound)
{
    public const string DefaultLocale = "ru-RU";

    public static NotificationSettings Default { get; } = new(
        BuildDefaultCategories(),
        QuietHours.Default,
        DndEnabled: false,
        Locale: DefaultLocale,
        Sound: true);

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

    public static NotificationSettings FromLegacy(
        bool newMessages,
        bool sound,
        bool disciplineChatMessages,
        bool mentions)
    {
        var copy = new Dictionary<int, ChannelToggle>(BuildDefaultCategories())
        {
            [NotificationCategoryCode.ChatMessageDirect] = newMessages ? ChannelToggle.AllOn : ChannelToggle.AllOff,
            [NotificationCategoryCode.ChatMessageDiscipline] = disciplineChatMessages ? ChannelToggle.AllOn : ChannelToggle.AllOff,
            [NotificationCategoryCode.ChatMessageMention] = mentions ? ChannelToggle.AllOn : ChannelToggle.AllOff,
        };

        return new NotificationSettings(copy, QuietHours.Default, DndEnabled: false, DefaultLocale, sound);
    }

    private static Dictionary<int, ChannelToggle> BuildDefaultCategories()
    {
        var dict = new Dictionary<int, ChannelToggle>(NotificationCategoryCode.All.Count);
        foreach (var code in NotificationCategoryCode.All)
        {
            dict[code] = ChannelToggle.AllOn;
        }

        return dict;
    }
}
