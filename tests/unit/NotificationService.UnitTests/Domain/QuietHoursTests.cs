using FluentAssertions;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace NotificationService.UnitTests.Domain;

public sealed class QuietHoursTests
{
    private const string Yekaterinburg = "Asia/Yekaterinburg";

    [Fact]
    public void Disabled_NeverActive()
    {
        var quiet = QuietHours.Disabled(Yekaterinburg);

        quiet.IsActive(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Enabled_ReturnsTrue_InsideWindow()
    {
        var quiet = QuietHours.Create(Yekaterinburg, new TimeOnly(22, 0), new TimeOnly(8, 0));

        // 23:30 local Yekaterinburg = 18:30 UTC
        var insideWindow = new DateTimeOffset(2026, 4, 26, 18, 30, 0, TimeSpan.Zero);

        quiet.IsActive(insideWindow).Should().BeTrue();
    }

    [Fact]
    public void Enabled_ReturnsFalse_OutsideWindow()
    {
        var quiet = QuietHours.Create(Yekaterinburg, new TimeOnly(22, 0), new TimeOnly(8, 0));

        // 12:00 local Yekaterinburg = 07:00 UTC
        var outsideWindow = new DateTimeOffset(2026, 4, 26, 7, 0, 0, TimeSpan.Zero);

        quiet.IsActive(outsideWindow).Should().BeFalse();
    }

    [Fact]
    public void Window_HandlesMidnightCrossing()
    {
        var quiet = QuietHours.Create(Yekaterinburg, new TimeOnly(22, 0), new TimeOnly(8, 0));

        // 02:00 local Yekaterinburg = 21:00 UTC previous day
        var earlyMorning = new DateTimeOffset(2026, 4, 26, 21, 0, 0, TimeSpan.Zero);

        quiet.IsActive(earlyMorning).Should().BeTrue();
    }

    [Fact]
    public void Window_WithoutMidnightCrossing_Works()
    {
        // 13:00 - 14:00 lunch quiet hours
        var quiet = QuietHours.Create(Yekaterinburg, new TimeOnly(13, 0), new TimeOnly(14, 0));

        // 13:30 local = 08:30 UTC
        var inside = new DateTimeOffset(2026, 4, 26, 8, 30, 0, TimeSpan.Zero);
        // 15:00 local = 10:00 UTC
        var outside = new DateTimeOffset(2026, 4, 26, 10, 0, 0, TimeSpan.Zero);

        quiet.IsActive(inside).Should().BeTrue();
        quiet.IsActive(outside).Should().BeFalse();
    }

    [Fact]
    public void Create_RejectsEqualStartAndEnd()
    {
        var act = () => QuietHours.Create(Yekaterinburg, new TimeOnly(22, 0), new TimeOnly(22, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/A_Real_Zone")]
    public void Create_RejectsInvalidTimezone(string timezone)
    {
        var act = () => QuietHours.Create(timezone, new TimeOnly(22, 0), new TimeOnly(8, 0));

        act.Should().Throw<ArgumentException>();
    }
}
