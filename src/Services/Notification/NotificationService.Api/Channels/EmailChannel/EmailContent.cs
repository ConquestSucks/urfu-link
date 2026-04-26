namespace Urfu.Link.Services.Notification.Channels.EmailChannel;

public sealed record EmailContent(string Subject, string Html, string Plain);

public sealed record EmailModel(
    string Title,
    string Body,
    string? DeepLink,
    string? ImageUrl,
    string Locale,
    IReadOnlyDictionary<string, string> Data);

public sealed record SmtpOptions
{
    public const string SectionName = "Notification:Smtp";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 25;

    public bool EnableStartTls { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string From { get; init; } = "noreply@urfu-link.local";

    public string FromDisplayName { get; init; } = "UrFU Link";
}
