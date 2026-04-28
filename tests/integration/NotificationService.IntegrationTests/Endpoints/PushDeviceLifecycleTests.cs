using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Endpoints;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace NotificationService.IntegrationTests.Endpoints;

/// <summary>
/// REST flow for the push device registry. Mobile clients retry registration on every
/// app launch; the contract is that POST is idempotent (returns the existing record on
/// duplicate token) and DELETE is also idempotent (no 404 if the device is already gone).
/// Both apply via Idempotency-Key required on POST.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class PushDeviceLifecycleTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    private readonly NotificationServiceFactory _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_persists_active_device_in_database()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var request = new RegisterPushDeviceRequest(
            Provider: PushProvider.Fcm,
            Token: "test-fcm-token",
            DeviceFingerprint: "fingerprint-1",
            Platform: "ios",
            AppVersion: "1.2.3",
            Locale: "ru-RU");

        var response = await client.PostAsJsonAsync("/api/v1/me/notifications/devices", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var stored = await db.PushDevices.AsNoTracking().Where(d => d.UserId == userId).SingleAsync();
        stored.Token.Should().Be("test-fcm-token");
        stored.IsActive.Should().BeTrue();
        stored.Provider.Should().Be(PushProvider.Fcm);
    }

    [Fact]
    public async Task Register_with_same_token_touches_existing_device_instead_of_inserting_duplicate()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        using var client = _factory.CreateClient();
        var request = new RegisterPushDeviceRequest(
            PushProvider.Apns, "shared-token", "fp", "ios", "1.0", "ru-RU");

        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var first = await client.PostAsJsonAsync("/api/v1/me/notifications/devices", request);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var second = await client.PostAsJsonAsync("/api/v1/me/notifications/devices", request);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var devices = await db.PushDevices.AsNoTracking().Where(d => d.UserId == userId).ToListAsync();
        devices.Should().HaveCount(1, "the second register call must not create a duplicate row.");
    }

    [Fact]
    public async Task Register_without_idempotency_key_returns_bad_request()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestAuthHandler.Principal(userId);

        using var client = _factory.CreateClient();
        var request = new RegisterPushDeviceRequest(
            PushProvider.Fcm, "token", "fp", "android", null, null);

        var response = await client.PostAsJsonAsync("/api/v1/me/notifications/devices", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "register endpoint requires Idempotency-Key to dedupe app-launch retries.");
    }
}
