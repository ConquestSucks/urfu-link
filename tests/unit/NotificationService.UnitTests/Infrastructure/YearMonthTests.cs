using FluentAssertions;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace NotificationService.UnitTests.Infrastructure;

public sealed class YearMonthTests
{
    [Fact]
    public void FromUtc_TakesYearAndMonth()
    {
        var ym = YearMonth.FromUtc(new DateTimeOffset(2026, 4, 26, 12, 0, 0, TimeSpan.Zero));

        ym.Should().Be(new YearMonth(2026, 4));
    }

    [Theory]
    [InlineData(2026, 4, 1, 2026, 5)]
    [InlineData(2026, 12, 1, 2027, 1)]
    [InlineData(2026, 1, -1, 2025, 12)]
    [InlineData(2026, 4, 0, 2026, 4)]
    public void AddMonths_RollsOverYearBoundaries(int year, int month, int delta, int expectedYear, int expectedMonth)
    {
        var ym = new YearMonth(year, month).AddMonths(delta);

        ym.Should().Be(new YearMonth(expectedYear, expectedMonth));
    }

    [Fact]
    public void StartUtc_AndNextStartUtc_AreContiguous()
    {
        var current = new YearMonth(2026, 4);

        current.StartUtc().Should().Be(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        current.NextStartUtc().Should().Be(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(2026, 4, "y2026m04")]
    [InlineData(2026, 12, "y2026m12")]
    [InlineData(2030, 1, "y2030m01")]
    public void PartitionSuffix_FormatsZeroPaddedMonth(int year, int month, string expected)
    {
        new YearMonth(year, month).PartitionSuffix().Should().Be(expected);
    }

    [Fact]
    public void Comparison_OrdersByYearThenMonth()
    {
        (new YearMonth(2026, 4) < new YearMonth(2026, 5)).Should().BeTrue();
        (new YearMonth(2026, 12) < new YearMonth(2027, 1)).Should().BeTrue();
        (new YearMonth(2026, 4) >= new YearMonth(2026, 4)).Should().BeTrue();
    }
}
