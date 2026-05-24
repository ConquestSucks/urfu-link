using FluentAssertions;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Domain;

public sealed class PushDeviceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Register_TrimsAndStoresAttributes()
    {
        var now = DateTimeOffset.UtcNow;

        var device = PushDevice.Register(
            UserId,
            PushProvider.Fcm,
            "  fcm-token  ",
            "device-fp-1",
            "android",
            "1.2.3",
            "ru-RU",
            now);

        device.Id.Should().NotBe(Guid.Empty);
        device.UserId.Should().Be(UserId);
        device.Provider.Should().Be(PushProvider.Fcm);
        device.Token.Should().Be("fcm-token");
        device.DeviceFingerprint.Should().Be("device-fp-1");
        device.Platform.Should().Be("android");
        device.AppVersion.Should().Be("1.2.3");
        device.Locale.Should().Be("ru-RU");
        device.CreatedAtUtc.Should().Be(now);
        device.LastSeenAtUtc.Should().Be(now);
        device.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Register_DefaultLocaleIsRuRU()
    {
        var device = PushDevice.Register(
            UserId,
            PushProvider.Apns,
            "apns-token",
            "fp",
            "ios",
            appVersion: null,
            locale: null,
            registeredAtUtc: DateTimeOffset.UtcNow);

        device.Locale.Should().Be("ru-RU");
    }

    [Fact]
    public void Touch_UpdatesLastSeen_AndKeepsActive()
    {
        var device = NewDevice(out var registeredAt);
        var now = registeredAt.AddHours(2);

        device.Touch(now);

        device.LastSeenAtUtc.Should().Be(now);
        device.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Touch_RejectsTimeBeforeRegistration()
    {
        var device = NewDevice(out var registeredAt);

        var act = () => device.Touch(registeredAt.AddMinutes(-1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_FlipsIsActive()
    {
        var device = NewDevice(out _);

        device.Deactivate(DateTimeOffset.UtcNow.AddDays(1), "fcm_unregistered");

        device.IsActive.Should().BeFalse();
        device.DeactivationReason.Should().Be("fcm_unregistered");
    }

    [Fact]
    public void Deactivate_IsIdempotent()
    {
        var device = NewDevice(out _);
        device.Deactivate(DateTimeOffset.UtcNow.AddDays(1), "fcm_unregistered");

        var act = () => device.Deactivate(DateTimeOffset.UtcNow.AddDays(2), "second");

        act.Should().NotThrow();
        device.DeactivationReason.Should().Be("fcm_unregistered");
    }

    [Fact]
    public void Reactivate_ClearsDeactivationStateAndUpdatesToken()
    {
        var device = NewDevice(out var registeredAt);
        device.Deactivate(registeredAt.AddDays(1), "expired");

        device.Reactivate("new-token", registeredAt.AddDays(2));

        device.IsActive.Should().BeTrue();
        device.Token.Should().Be("new-token");
        device.DeactivationReason.Should().BeNull();
        device.LastSeenAtUtc.Should().Be(registeredAt.AddDays(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_RejectsBlankToken(string token)
    {
        var act = () => PushDevice.Register(
            UserId,
            PushProvider.Fcm,
            token,
            "fp",
            "android",
            null,
            null,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_RejectsEmptyUserId()
    {
        var act = () => PushDevice.Register(
            Guid.Empty,
            PushProvider.Fcm,
            "tok",
            "fp",
            "android",
            null,
            null,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    private static PushDevice NewDevice(out DateTimeOffset registeredAt)
    {
        registeredAt = new DateTimeOffset(2026, 4, 26, 12, 0, 0, TimeSpan.Zero);
        return PushDevice.Register(UserId, PushProvider.Fcm, "token", "fp", "android", "1.0.0", "ru-RU", registeredAt);
    }
}
