using FluentAssertions;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace NotificationService.UnitTests.Domain;

public sealed class NotificationContentTests
{
    [Fact]
    public void Create_TrimsTitleAndBody()
    {
        var content = NotificationContent.Create("  Hello  ", "  Body text  ");

        content.Title.Should().Be("Hello");
        content.Body.Should().Be("Body text");
        content.ImageUrl.Should().BeNull();
        content.DeepLink.Should().BeNull();
    }

    [Fact]
    public void Create_AcceptsOptionalImageAndDeepLink()
    {
        var content = NotificationContent.Create(
            "Title",
            "Body",
            imageUrl: "https://cdn.example/image.png",
            deepLink: "urfulink://chat/conv/abc");

        content.ImageUrl.Should().Be("https://cdn.example/image.png");
        content.DeepLink.Should().Be("urfulink://chat/conv/abc");
    }

    [Theory]
    [InlineData("", "Body")]
    [InlineData("   ", "Body")]
    [InlineData(null, "Body")]
    [InlineData("Title", "")]
    [InlineData("Title", "   ")]
    [InlineData("Title", null)]
    public void Create_RejectsBlankTitleOrBody(string? title, string? body)
    {
        var act = () => NotificationContent.Create(title!, body!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsTitleAboveLimit()
    {
        var oversizeTitle = new string('a', NotificationContent.TitleMaxLength + 1);

        var act = () => NotificationContent.Create(oversizeTitle, "Body");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_RejectsBodyAboveLimit()
    {
        var oversizeBody = new string('a', NotificationContent.BodyMaxLength + 1);

        var act = () => NotificationContent.Create("Title", oversizeBody);

        act.Should().Throw<ArgumentException>();
    }
}
