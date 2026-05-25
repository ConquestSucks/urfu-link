using System.Globalization;
using Grpc.Core;
using Urfu.Link.Services.User.Grpc;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Search;
using DomainChannelToggle = UserService.Api.Domain.ValueObjects.ChannelToggle;
using DomainQuietHours = UserService.Api.Domain.ValueObjects.QuietHours;
using DomainSettings = UserService.Api.Domain.ValueObjects.NotificationSettings;

namespace UserService.Api.Services;

public sealed class InternalApiService(
    IUserRepository userRepository,
    IUserDirectory userDirectory,
    IUserSearchRepository userSearchRepository,
    UserSearchTextBuilder userSearchTextBuilder) : InternalApi.InternalApiBase
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

        // Стратегия:
        //   1) Локальная проекция user_search_projection — горячий путь, ~ms.
        //      Наполняется UserSearchReconciler-ом и lazy-upsert-ом на логине.
        //   2) Для id, отсутствующих в проекции — fallback на Keycloak Admin API
        //      (источник истины). Результат немедленно UPSERT-им обратно в проекцию,
        //      чтобы следующий BatchGetUsers по тому же id уже шёл из (1).
        //   3) Аватары — всегда из локального user_profiles (там реальный AvatarUrl).
        // userSearchRepository и userRepository шарят один scoped DbContext —
        // запросы выполняем последовательно, иначе EF падает с "A second operation
        // was started on this context instance".
        var ct = context.CancellationToken;
        var projection = await userSearchRepository.BatchGetSummariesAsync(userIds, ct).ConfigureAwait(false);
        var avatars = await userRepository.GetAvatarUrlsAsync(userIds, ct).ConfigureAwait(false);

        // Из проекции принимаем запись только если у неё есть непустое DisplayName.
        // Пустой DisplayName в проекции — артефакт: либо lazy-upsert на логине без
        // полноценных JWT-клеймов, либо ранний UPSERT, когда KC ещё не вернул данные.
        // Такой id считаем "missing" и идём в KC заново.
        var missing = userIds
            .Where(id => !projection.TryGetValue(id, out var s)
                || string.IsNullOrWhiteSpace(s.DisplayName))
            .ToArray();
        IReadOnlyDictionary<Guid, UserDirectoryEntry> directory =
            new Dictionary<Guid, UserDirectoryEntry>();
        if (missing.Length > 0)
        {
            directory = await userDirectory.GetUsersAsync(missing, ct).ConfigureAwait(false);

            // Fire-and-forget UPSERT для прогрева проекции. Пишем только записи
            // с непустым DisplayName, чтобы не кэшировать "мусор" и не блокировать
            // последующие fallback'и на KC.
            foreach (var (id, entry) in directory)
            {
                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                    continue;

                var (searchText, translit) = userSearchTextBuilder.Build(
                    username: entry.DisplayName,
                    firstName: null,
                    lastName: null,
                    email: entry.Email);

                _ = userSearchRepository.UpsertAsync(
                    new UserSearchUpsert(
                        UserId: id,
                        Username: entry.DisplayName,
                        FirstName: null,
                        LastName: null,
                        Email: string.IsNullOrWhiteSpace(entry.Email) ? null : entry.Email,
                        DisplayName: entry.DisplayName,
                        SearchText: searchText,
                        SearchTextTranslit: translit,
                        KeycloakModifiedMs: 0),
                    CancellationToken.None);
            }
        }

        foreach (var id in userIds)
        {
            string displayName = string.Empty;
            string email = string.Empty;

            // Приоритет: проекция (если есть непустое имя) → KC → fallback на short-id.
            if (projection.TryGetValue(id, out var summary)
                && !string.IsNullOrWhiteSpace(summary.DisplayName))
            {
                displayName = summary.DisplayName;
                email = summary.Email;
            }
            else if (directory.TryGetValue(id, out var entry)
                && !string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                displayName = entry.DisplayName;
                email = entry.Email;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                // Гарантируем non-empty: UI никогда не должен получать "".
                displayName = $"User {id.ToString()[..8]}";
            }

            avatars.TryGetValue(id, out var avatar);
            reply.Users.Add(new UserSummary
            {
                UserId = id.ToString(),
                DisplayName = displayName,
                Email = email,
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

        payload.MutedConversationIds.AddRange(settings.MutedConversationIds);

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
            payload.Sound,
            payload.MutedConversationIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }
}
