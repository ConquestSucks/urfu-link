namespace Urfu.Link.Services.Notification.Domain.ValueObjects;

public sealed record NotificationContent
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 1000;
    public const int UrlMaxLength = 2048;

    public string Title { get; }

    public string Body { get; }

    public string? ImageUrl { get; }

    public string? DeepLink { get; }

    private NotificationContent(string title, string body, string? imageUrl, string? deepLink)
    {
        Title = title;
        Body = body;
        ImageUrl = imageUrl;
        DeepLink = deepLink;
    }

    public static NotificationContent Create(
        string title,
        string body,
        string? imageUrl = null,
        string? deepLink = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var trimmedTitle = title.Trim();
        var trimmedBody = body.Trim();

        if (trimmedTitle.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Title exceeds {TitleMaxLength} characters.", nameof(title));
        }

        if (trimmedBody.Length > BodyMaxLength)
        {
            throw new ArgumentException($"Body exceeds {BodyMaxLength} characters.", nameof(body));
        }

        var trimmedImage = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        var trimmedLink = string.IsNullOrWhiteSpace(deepLink) ? null : deepLink.Trim();

        if (trimmedImage is not null && trimmedImage.Length > UrlMaxLength)
        {
            throw new ArgumentException($"Image URL exceeds {UrlMaxLength} characters.", nameof(imageUrl));
        }

        if (trimmedLink is not null && trimmedLink.Length > UrlMaxLength)
        {
            throw new ArgumentException($"Deep link exceeds {UrlMaxLength} characters.", nameof(deepLink));
        }

        return new NotificationContent(trimmedTitle, trimmedBody, trimmedImage, trimmedLink);
    }
}
