using FluentAssertions;
using Urfu.Link.Services.Notification.Channels.EmailChannel;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Channels;

public sealed class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _renderer = new();

    [Theory]
    [InlineData(NotificationCategory.ChatMessageDirect, "ru-RU", "Новое сообщение")]
    [InlineData(NotificationCategory.ChatMessageMention, "ru-RU", "Вас упомянули")]
    [InlineData(NotificationCategory.CallIncoming, "ru-RU", "Входящий звонок")]
    [InlineData(NotificationCategory.DisciplineDeadline, "ru-RU", "Скоро дедлайн")]
    [InlineData(NotificationCategory.AdminRoleChanged, "ru-RU", "Изменение роли")]
    public void Renders_RuTemplate_WithCategoryHeader(NotificationCategory category, string locale, string expectedHeader)
    {
        var model = new EmailModel(
            "Hello",
            "World",
            "urfulink://x",
            null,
            locale,
            new Dictionary<string, string>());

        var content = _renderer.Render(category, locale, model);

        content.Subject.Should().StartWith("[UrFU Link]");
        content.Html.Should().Contain(expectedHeader);
        content.Plain.Should().Contain(expectedHeader);
        content.Html.Should().Contain("Hello");
        content.Plain.Should().Contain("World");
    }

    [Theory]
    [InlineData(NotificationCategory.ChatMessageDirect, "en-US", "New message")]
    [InlineData(NotificationCategory.CallMissed, "en-US", "Missed call")]
    [InlineData(NotificationCategory.SystemMaintenance, "en-US", "Scheduled maintenance")]
    public void Renders_EnTemplate_WithCategoryHeader(NotificationCategory category, string locale, string expectedHeader)
    {
        var model = new EmailModel(
            "Hello",
            "World",
            "urfulink://x",
            null,
            locale,
            new Dictionary<string, string>());

        var content = _renderer.Render(category, locale, model);

        content.Html.Should().Contain(expectedHeader);
        content.Plain.Should().Contain(expectedHeader);
    }

    [Fact]
    public void UnknownLocale_FallsBackToRu()
    {
        var model = new EmailModel("T", "B", null, null, "fr-FR", new Dictionary<string, string>());

        var content = _renderer.Render(NotificationCategory.ChatMessageDirect, "fr-FR", model);

        content.Html.Should().Contain("Новое сообщение");
    }

    [Fact]
    public void RendersEveryCategoryForBothLocales()
    {
        var model = new EmailModel("T", "B", null, null, "ru-RU", new Dictionary<string, string>());

        foreach (var locale in new[] { "ru-RU", "en-US" })
        {
            foreach (var category in Enum.GetValues<NotificationCategory>())
            {
                var content = _renderer.Render(category, locale, model);
                content.Html.Should().NotBeNullOrEmpty();
                content.Plain.Should().NotBeNullOrEmpty();
            }
        }
    }
}
