using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Channels.EmailChannel;

public interface ITemplateRenderer
{
    EmailContent Render(NotificationCategory category, string locale, EmailModel model);
}

/// <summary>
/// Renders embedded HTML/plain templates per (category, locale). Placeholder syntax is
/// <c>{{Title}}</c>, <c>{{Body}}</c>, <c>{{DeepLink}}</c>, <c>{{ImageUrl}}</c>, plus
/// <c>{{data.key}}</c> for arbitrary payload keys. The renderer falls back to ru-RU if
/// the requested locale lacks a template, and to a default template if the category
/// lacks a per-category file.
/// </summary>
public sealed partial class EmailTemplateRenderer : ITemplateRenderer
{
    private const string DefaultLocale = "ru-RU";
    private static readonly string ResourcePrefix = typeof(EmailTemplateRenderer).Namespace + ".Templates.";
    private readonly Assembly _assembly = typeof(EmailTemplateRenderer).Assembly;

    public EmailContent Render(NotificationCategory category, string locale, EmailModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var html = LoadTemplate(category, locale, "html") ?? LoadTemplate(category, DefaultLocale, "html") ?? DefaultHtml(model);
        var txt = LoadTemplate(category, locale, "txt") ?? LoadTemplate(category, DefaultLocale, "txt") ?? DefaultPlain(model);

        var subject = $"[UrFU Link] {model.Title}";
        return new EmailContent(subject, Substitute(html, model, htmlEncode: true), Substitute(txt, model, htmlEncode: false));
    }

    private string? LoadTemplate(NotificationCategory category, string locale, string format)
    {
        var resourceName = $"{ResourcePrefix}{locale}.{category}.{format}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Substitute(string template, EmailModel model, bool htmlEncode)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = model.Title,
            ["body"] = model.Body,
            ["deepLink"] = model.DeepLink ?? string.Empty,
            ["imageUrl"] = model.ImageUrl ?? string.Empty,
            ["locale"] = model.Locale,
        };

        foreach (var (key, value) in model.Data)
        {
            values[$"data.{key}"] = value;
        }

        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (!values.TryGetValue(key, out var v))
            {
                return string.Empty;
            }

            return htmlEncode ? WebUtility.HtmlEncode(v) : v;
        });
    }

    private static string DefaultHtml(EmailModel model)
    {
        var title = WebUtility.HtmlEncode(model.Title);
        var body = WebUtility.HtmlEncode(model.Body);
        var link = WebUtility.HtmlEncode(model.DeepLink ?? string.Empty);
        var cta = string.IsNullOrWhiteSpace(model.DeepLink)
            ? string.Empty
            : $"<p><a href=\"{link}\">Открыть</a></p>";
        return $"<html><body><h1>{title}</h1><p>{body}</p>{cta}</body></html>";
    }

    private static string DefaultPlain(EmailModel model) => $"""
        {model.Title}

        {model.Body}
        {(string.IsNullOrWhiteSpace(model.DeepLink) ? string.Empty : $"Перейти: {model.DeepLink}")}
        """;

    [GeneratedRegex(@"\{\{\s*([\w\.]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}
