using System.Collections.Concurrent;
using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.User.Grpc;
using ChannelToggle = Urfu.Link.Services.Notification.Application.Preferences.ChannelToggle;
using DomainQuietHours = Urfu.Link.Services.Notification.Domain.ValueObjects.QuietHours;
using GrpcChannelToggle = Urfu.Link.Services.User.Grpc.ChannelToggle;
using GrpcCategoryToggle = Urfu.Link.Services.User.Grpc.CategoryToggle;
using GrpcQuietHours = Urfu.Link.Services.User.Grpc.QuietHours;

namespace Urfu.Link.Services.Notification.Infrastructure.Grpc;

/// <summary>
/// gRPC-backed implementation of <see cref="IUserPreferencesClient"/> with a per-user
/// in-memory cache invalidated by <c>UserNotificationSettingsChangedEvent</c>.
/// </summary>
public sealed class UserServiceClient(
    InternalApi.InternalApiClient client,
    IOptions<UserServiceClientOptions> options,
    TimeProvider timeProvider,
    ILogger<UserServiceClient> logger) : IUserPreferencesClient
{
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();

    public async Task<UserPreferences> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(userId, out var entry) && entry.ExpiresAtUtc > timeProvider.GetUtcNow())
        {
            return entry.Preferences;
        }

        try
        {
            var reply = await client.GetNotificationPreferencesAsync(
                new GetNotificationPreferencesRequest { UserId = userId.ToString() },
                cancellationToken: cancellationToken);

            var preferences = MapPreferences(reply.Preferences);
            _cache[userId] = new CacheEntry(preferences, timeProvider.GetUtcNow() + options.Value.PreferencesCacheTtl);
            return preferences;
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "UserService preferences fetch failed for {UserId}; falling back to default", userId);
            return UserPreferences.Default;
        }
    }

    public async Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await client.GetUserContactAsync(
                new GetUserContactRequest { UserId = userId.ToString() },
                cancellationToken: cancellationToken);

            return new UserContact(
                reply.Email ?? string.Empty,
                reply.DisplayName ?? string.Empty,
                string.IsNullOrWhiteSpace(reply.Locale) ? "ru-RU" : reply.Locale);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "UserService contact fetch failed for {UserId}; falling back to empty", userId);
            return new UserContact(string.Empty, string.Empty, "ru-RU");
        }
    }

    public async Task<UserPreferences> UpdateAsync(Guid userId, UserPreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var payload = MapToGrpc(preferences);
        await client.UpdateNotificationPreferencesAsync(
            new UpdateNotificationPreferencesRequest
            {
                UserId = userId.ToString(),
                Preferences = payload,
            },
            cancellationToken: cancellationToken);

        Invalidate(userId);
        return preferences;
    }

    public void Invalidate(Guid userId) => _cache.TryRemove(userId, out _);

    private static UserPreferences MapPreferences(NotificationPreferencesPayload payload)
    {
        if (payload is null)
        {
            return UserPreferences.Default;
        }

        var categories = new Dictionary<NotificationCategory, ChannelToggle>();
        foreach (var entry in payload.Categories)
        {
            if (!Enum.IsDefined((NotificationCategory)entry.Category))
            {
                continue;
            }

            categories[(NotificationCategory)entry.Category] = new ChannelToggle(
                entry.Toggle?.Push ?? true,
                entry.Toggle?.Email ?? true,
                entry.Toggle?.InApp ?? true);
        }

        var quietHours = payload.QuietHours is null || !payload.QuietHours.Enabled
            ? DomainQuietHours.Disabled(payload.QuietHours?.IanaTimezone ?? "Asia/Yekaterinburg")
            : DomainQuietHours.Create(
                payload.QuietHours.IanaTimezone,
                TimeOnly.ParseExact(payload.QuietHours.Start, "HH:mm", CultureInfo.InvariantCulture),
                TimeOnly.ParseExact(payload.QuietHours.End, "HH:mm", CultureInfo.InvariantCulture));

        return new UserPreferences(
            categories,
            quietHours,
            payload.DndEnabled,
            string.IsNullOrWhiteSpace(payload.Locale) ? "ru-RU" : payload.Locale,
            payload.Sound,
            payload.MutedConversationIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private static NotificationPreferencesPayload MapToGrpc(UserPreferences preferences)
    {
        var payload = new NotificationPreferencesPayload
        {
            DndEnabled = preferences.DndEnabled,
            Locale = preferences.Locale,
            Sound = preferences.Sound,
            QuietHours = new GrpcQuietHours
            {
                IanaTimezone = preferences.QuietHours.IanaTimezone,
                Start = preferences.QuietHours.Enabled
                    ? preferences.QuietHours.Start.ToString("HH:mm", CultureInfo.InvariantCulture)
                    : string.Empty,
                End = preferences.QuietHours.Enabled
                    ? preferences.QuietHours.End.ToString("HH:mm", CultureInfo.InvariantCulture)
                    : string.Empty,
                Enabled = preferences.QuietHours.Enabled,
            },
        };

        foreach (var (category, toggle) in preferences.Categories)
        {
            payload.Categories.Add(new GrpcCategoryToggle
            {
                Category = (int)category,
                Toggle = new GrpcChannelToggle
                {
                    Push = toggle.Push,
                    Email = toggle.Email,
                    InApp = toggle.InApp,
                },
            });
        }

        payload.MutedConversationIds.AddRange(preferences.MutedConversationIds);

        return payload;
    }

    private sealed record CacheEntry(UserPreferences Preferences, DateTimeOffset ExpiresAtUtc);
}
