using FluentAssertions;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace NotificationService.UnitTests.Application;

public sealed class PreferenceFilterTests
{
    private static readonly DateTimeOffset NoonUtc = new(2026, 4, 26, 7, 0, 0, TimeSpan.Zero); // Yekaterinburg 12:00
    private static readonly DateTimeOffset NightUtc = new(2026, 4, 26, 19, 0, 0, TimeSpan.Zero); // Yekaterinburg 00:00

    [Fact]
    public void NoChannelDisabled_ReturnsAllCandidates()
    {
        var prefs = UserPreferences.Default;
        var candidates = SeverityRouter.Select(NotificationSeverity.High);

        var result = PreferenceFilter.Filter(
            candidates,
            NotificationCategory.ChatMessageDirect,
            NotificationSeverity.High,
            prefs,
            NoonUtc);

        result.Should().BeEquivalentTo(candidates);
    }

    [Fact]
    public void DisabledPushToggle_RemovesPush()
    {
        var prefs = UserPreferences.Default with
        {
            Categories = new Dictionary<NotificationCategory, ChannelToggle>
            {
                [NotificationCategory.ChatMessageDirect] = new(false, true, true),
            },
        };

        var result = PreferenceFilter.Filter(
            SeverityRouter.Select(NotificationSeverity.Normal),
            NotificationCategory.ChatMessageDirect,
            NotificationSeverity.Normal,
            prefs,
            NoonUtc);

        result.Should().NotContain(DeliveryChannel.Push);
        result.Should().Contain(DeliveryChannel.InApp);
    }

    [Fact]
    public void QuietHours_BlocksPushButKeepsInApp()
    {
        var prefs = UserPreferences.Default with
        {
            QuietHours = QuietHours.Create("Asia/Yekaterinburg", new TimeOnly(22, 0), new TimeOnly(8, 0)),
        };

        var result = PreferenceFilter.Filter(
            SeverityRouter.Select(NotificationSeverity.Normal),
            NotificationCategory.ChatMessageDirect,
            NotificationSeverity.Normal,
            prefs,
            NightUtc);

        result.Should().NotContain(DeliveryChannel.Push);
        result.Should().Contain(DeliveryChannel.InApp);
    }

    [Fact]
    public void Urgent_BypassesQuietHours()
    {
        var prefs = UserPreferences.Default with
        {
            QuietHours = QuietHours.Create("Asia/Yekaterinburg", new TimeOnly(22, 0), new TimeOnly(8, 0)),
        };

        var result = PreferenceFilter.Filter(
            SeverityRouter.Select(NotificationSeverity.Urgent),
            NotificationCategory.CallIncoming,
            NotificationSeverity.Urgent,
            prefs,
            NightUtc);

        result.Should().Contain(DeliveryChannel.Push);
    }

    [Fact]
    public void Dnd_BlocksPushUnlessUrgent()
    {
        var prefs = UserPreferences.Default with { DndEnabled = true };

        var resultNormal = PreferenceFilter.Filter(
            SeverityRouter.Select(NotificationSeverity.Normal),
            NotificationCategory.ChatMessageDirect,
            NotificationSeverity.Normal,
            prefs,
            NoonUtc);

        var resultUrgent = PreferenceFilter.Filter(
            SeverityRouter.Select(NotificationSeverity.Urgent),
            NotificationCategory.CallIncoming,
            NotificationSeverity.Urgent,
            prefs,
            NoonUtc);

        resultNormal.Should().NotContain(DeliveryChannel.Push);
        resultUrgent.Should().Contain(DeliveryChannel.Push);
    }

    [Fact]
    public void Email_OnlyEmittedForHighOrAbove()
    {
        var prefs = UserPreferences.Default;

        var normalResult = PreferenceFilter.Filter(
            [DeliveryChannel.InApp, DeliveryChannel.Push, DeliveryChannel.Email],
            NotificationCategory.ChatMessageDirect,
            NotificationSeverity.Normal,
            prefs,
            NoonUtc);

        var highResult = PreferenceFilter.Filter(
            [DeliveryChannel.InApp, DeliveryChannel.Push, DeliveryChannel.Email],
            NotificationCategory.ChatMessageMention,
            NotificationSeverity.High,
            prefs,
            NoonUtc);

        normalResult.Should().NotContain(DeliveryChannel.Email);
        highResult.Should().Contain(DeliveryChannel.Email);
    }
}
