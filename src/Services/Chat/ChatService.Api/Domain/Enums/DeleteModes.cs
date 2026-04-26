namespace Urfu.Link.Services.Chat.Domain.Enums;

/// <summary>
/// Wire-format constants and conversions for <see cref="DeleteMode"/>. The enum value
/// <c>ForEveryone</c> is serialised on the wire as <c>"for-everyone"</c> (kebab-case) so REST
/// query strings, hub method arguments, and SignalR broadcasts all share the same vocabulary.
/// </summary>
public static class DeleteModes
{
    public const string ForMe = "for-me";

    public const string ForEveryone = "for-everyone";

    /// <summary>
    /// Parses the wire value into a <see cref="DeleteMode"/>. <see langword="null"/> or empty
    /// defaults to <see cref="DeleteMode.ForMe"/> (the safer, local-only mode). Anything else
    /// is rejected with <see cref="ArgumentException"/>.
    /// </summary>
    public static DeleteMode Parse(string? value)
    {
        return value switch
        {
            ForEveryone => DeleteMode.ForEveryone,
            null or "" or ForMe => DeleteMode.ForMe,
            _ => throw new ArgumentException(
                $"Unsupported delete mode '{value}'. Use '{ForMe}' or '{ForEveryone}'.",
                nameof(value)),
        };
    }

    public static string ToWire(this DeleteMode mode) => mode switch
    {
        DeleteMode.ForEveryone => ForEveryone,
        DeleteMode.ForMe => ForMe,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown DeleteMode."),
    };
}
