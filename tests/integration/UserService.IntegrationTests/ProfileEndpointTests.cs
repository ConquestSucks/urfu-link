using System.Net;
using System.Net.Http.Json;
using UserService.Api.Application.Contracts.Responses;
using UserService.IntegrationTests.Helpers;

namespace UserService.IntegrationTests;

public sealed class ProfileEndpointTests(UserServiceFactory factory) : IClassFixture<UserServiceFactory>
{
    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        return client;
    }

    [Fact]
    public async Task GetProfileShouldReturnDefaultSettingsForNewUser()
    {
        var freshUserId = Guid.NewGuid();
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", freshUserId.ToString());
        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(freshUserId, profile.UserId);
        Assert.Null(profile.Account.AvatarUrl);
        Assert.Null(profile.Account.AboutMe);
        Assert.True(profile.Privacy.ShowOnlineStatus);
        Assert.True(profile.Privacy.ShowLastVisitTime);
        Assert.True(profile.Notifications.NewMessages);
        Assert.True(profile.Notifications.NotificationSound);
        Assert.True(profile.Notifications.DisciplineChatMessages);
        Assert.True(profile.Notifications.Mentions);
    }

    [Fact]
    public async Task GetProfileWithoutAuthShouldReturn401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAccountShouldPersistAboutMe()
    {
        var client = CreateAuthenticatedClient();

        var updateResponse = await client.PutAsJsonAsync(
            new Uri("/api/v1/me/account", UriKind.Relative),
            new { AboutMe = "Test bio" });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var getResponse = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await getResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("Test bio", profile?.Account.AboutMe);
    }

    [Fact]
    public async Task UpdateAccountWithLongAboutMeShouldReturn400()
    {
        var client = CreateAuthenticatedClient();

        var updateResponse = await client.PutAsJsonAsync(
            new Uri("/api/v1/me/account", UriKind.Relative),
            new { AboutMe = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task UpdatePrivacyShouldPersistSettings()
    {
        var client = CreateAuthenticatedClient();

        var updateResponse = await client.PutAsJsonAsync(
            new Uri("/api/v1/me/privacy", UriKind.Relative),
            new { ShowOnlineStatus = false, ShowLastVisitTime = false });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var getResponse = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await getResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.False(profile?.Privacy.ShowOnlineStatus);
        Assert.False(profile?.Privacy.ShowLastVisitTime);
    }

    [Fact]
    public async Task UpdateNotificationsShouldPersistSettings()
    {
        var client = CreateAuthenticatedClient();

        var updateResponse = await client.PutAsJsonAsync(
            new Uri("/api/v1/me/notifications", UriKind.Relative),
            new
            {
                NewMessages = false,
                NotificationSound = true,
                DisciplineChatMessages = false,
                Mentions = true,
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var getResponse = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await getResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.False(profile?.Notifications.NewMessages);
        Assert.True(profile?.Notifications.NotificationSound);
        Assert.False(profile?.Notifications.DisciplineChatMessages);
        Assert.True(profile?.Notifications.Mentions);
    }

    [Fact]
    public async Task PatchSoundVideoShouldPersistAllFields()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());

        var updateResponse = await client.PatchAsJsonAsync(
            new Uri("/api/v1/me/sound-video", UriKind.Relative),
            new
            {
                PlaybackDeviceId = "speaker-1",
                RecordingDeviceId = "mic-1",
                WebcamDeviceId = "cam-1",
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var getResponse = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await getResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("speaker-1", profile?.SoundVideo.PlaybackDeviceId);
        Assert.Equal("mic-1", profile?.SoundVideo.RecordingDeviceId);
        Assert.Equal("cam-1", profile?.SoundVideo.WebcamDeviceId);
    }

    [Fact]
    public async Task PatchSoundVideoShouldUpdateOnlyProvidedFields()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());

        await client.PatchAsJsonAsync(
            new Uri("/api/v1/me/sound-video", UriKind.Relative),
            new { PlaybackDeviceId = "speaker-1", RecordingDeviceId = "mic-1", WebcamDeviceId = "cam-1" });

        var partialResponse = await client.PatchAsJsonAsync(
            new Uri("/api/v1/me/sound-video", UriKind.Relative),
            new { RecordingDeviceId = "mic-2" });

        Assert.Equal(HttpStatusCode.NoContent, partialResponse.StatusCode);

        var getResponse = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await getResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("speaker-1", profile?.SoundVideo.PlaybackDeviceId);
        Assert.Equal("mic-2", profile?.SoundVideo.RecordingDeviceId);
        Assert.Equal("cam-1", profile?.SoundVideo.WebcamDeviceId);
    }
}
