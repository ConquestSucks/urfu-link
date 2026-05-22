using System.Globalization;
using Grpc.Core;
using Urfu.Link.Services.User.Grpc;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using DomainChannelToggle = UserService.Api.Domain.ValueObjects.ChannelToggle;
using DomainQuietHours = UserService.Api.Domain.ValueObjects.QuietHours;
using DomainSettings = UserService.Api.Domain.ValueObjects.NotificationSettings;

namespace UserService.Api.Services;

public sealed class InternalApiService(
    IUserRepository userRepository,
    IUserDirectory userDirectory) : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = context;
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "user-service",
            Utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        });
    }

    public override async Task<GetNotificationPreferencesReply> GetNotificationPreferences(
        GetNotificationPreferencesRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var userId = ParseGuid(request.UserId, nameof(request.UserId));
        var profile = await userRepository.GetByIdAsync(userId, context.CancellationToken).ConfigureAwait(false);
        var settings = profile?.Notifications ?? DomainSettings.Default;

        return new GetNotificationPreferencesReply { Preferences = MapPreferences(settings) };
    }

    public override async Task<UpdateNotificationPreferencesReply> UpdateNotificationPreferences(
        UpdateNotificationPreferencesRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (request.Preferences is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Preferences payload is required."));
        }

        var userId = ParseGuid(request.UserId, nameof(request.UserId));
        var profile = await userRepository.GetByIdAsync(userId, context.CancellationToken).ConfigureAwait(false)
                      ?? UserProfile.CreateDefault(userId);

        var settings = MapPreferences(request.Preferences);
        profile.UpdateNotificationPreferences(settings);
        if (await userRepository.GetByIdAsync(userId, context.CancellationToken).ConfigureAwait(false) is null)
        {
            userRepository.Add(profile);
        }

        await userRepository.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

        return new UpdateNotificationPreferencesReply { Preferences = MapPreferences(profile.Notifications) };
    }

    public override async Task<GetUserContactReply> GetUserContact(GetUserContactRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var userId = ParseGuid(request.UserId, nameof(request.UserId));
        var entries = await userDirectory.GetUsersAsync(new[] { userId }, context.CancellationToken).ConfigureAwait(false);

        if (!entries.TryGetValue(userId, out var entry))
        {
            return new GetUserContactReply
            {
                Email = string.Empty,
                DisplayName = string.Empty,
                Locale = DomainSettings.DefaultLocale,
            };
        }

        return new GetUserContactReply
        {
            Email = entry.Email,
            DisplayName = entry.DisplayName,
            Locale = DomainSettings.DefaultLocale,
        };
    }

    public override async Task<BatchGetUsersReply> BatchGetUsers(BatchGetUsersRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var userIds = request.UserIds
            .Select(raw => ParseGuid(raw, nameof(request.UserIds)))
            .Distinct()
            .ToArray();

        var reply = new BatchGetUsersReply();
        if (userIds.Length == 0)
            return reply;

        // Параллельно: контакты из Keycloak (источник истины по имени/email) и
        // аватары из локальной БД UserService (там, где Account.AvatarUrl).
        var directoryTask = userDirectory.GetUsersAsync(userIds, context.CancellationToken);
        var avatarsTask = userRepository.GetAvatarUrlsAsync(userIds, context.CancellationToken);
        var directory = await directoryTask.ConfigureAwait(false);
        var avatars = await avatarsTask.ConfigureAwait(false);

        foreach (var id in userIds)
        {
            directory.TryGetValue(id, out var entry);
            avatars.TryGetValue(id, out var avatar);
            reply.Users.Add(new UserSummary
            {
                UserId = id.ToString(),
                DisplayName = entry?.DisplayName ?? string.Empty,
                Email = entry?.Email ?? string.Empty,
                AvatarUrl = avatar ?? string.Empty,
            });
        }

        return reply;
    }

    private static Guid ParseGuid(string raw, string parameterName)
    {
        if (!Guid.TryParse(raw, out var parsed) || parsed == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{parameterName} must be a non-empty GUID."));
        }

        return parsed;
    }

    private static NotificationPreferencesPayload MapPreferences(DomainSettings settings)
    {
        var payload = new NotificationPreferencesPayload
        {
            DndEnabled = settings.DndEnabled,
            Locale = settings.Locale,
            Sound = settings.Sound,
            QuietHours = new Urfu.Link.Services.User.Grpc.QuietHours
            {
                IanaTimezone = settings.QuietHours.IanaTimezone,
                Start = settings.QuietHours.Start?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
                End = settings.QuietHours.End?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
                Enabled = settings.QuietHours.Enabled,
            },
        };

        foreach (var (category, toggle) in settings.Categories.OrderBy(kv => kv.Key))
        {
            payload.Categories.Add(new CategoryToggle
            {
                Category = category,
                Toggle = new Urfu.Link.Services.User.Grpc.ChannelToggle
                {
                    Push = toggle.Push,
                    Email = toggle.Email,
                    InApp = toggle.InApp,
                },
            });
        }

        return payload;
    }

    private static DomainSettings MapPreferences(NotificationPreferencesPayload payload)
    {
        var categories = payload.Categories.ToDictionary(
            c => c.Category,
            c => new DomainChannelToggle(c.Toggle?.Push ?? true, c.Toggle?.Email ?? true, c.Toggle?.InApp ?? true));

        var quietHours = payload.QuietHours is null || !payload.QuietHours.Enabled
            ? DomainQuietHours.Disabled(payload.QuietHours?.IanaTimezone ?? "Asia/Yekaterinburg")
            : DomainQuietHours.Create(
                payload.QuietHours.IanaTimezone,
                TimeOnly.ParseExact(payload.QuietHours.Start, "HH:mm", CultureInfo.InvariantCulture),
                TimeOnly.ParseExact(payload.QuietHours.End, "HH:mm", CultureInfo.InvariantCulture));

        return new DomainSettings(
            categories,
            quietHours,
            payload.DndEnabled,
            string.IsNullOrWhiteSpace(payload.Locale) ? DomainSettings.DefaultLocale : payload.Locale,
            payload.Sound);
    }
}
