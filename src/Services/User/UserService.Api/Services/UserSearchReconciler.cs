using Microsoft.Extensions.Options;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Search;

namespace UserService.Api.Services;

// Периодически синхронизирует проекцию users.user_search_projection с Keycloak.
// Запускается с задержкой start-up (60 секунд), чтобы дать сервису пройти health-check
// до начала обхода KC. Дальше — раз в ReconcileInterval.
//
// Альтернатива: Keycloak SPI event-listener с публикацией в Kafka. Требует поставки
// .jar в KC container — отложено (см. план в ~/.claude/plans/quiet-humming-gizmo.md).
public sealed class UserSearchReconciler(
    IServiceProvider serviceProvider,
    IOptions<UserSearchReconcilerOptions> options,
    ILogger<UserSearchReconciler> logger) : BackgroundService
{
    private readonly UserSearchReconcilerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("UserSearchReconciler is disabled by configuration.");
            return;
        }

        try
        {
            await Task.Delay(_options.StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // background service не должен умирать из-за единичного сбоя
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "UserSearchReconciler iteration failed.");
            }

            try
            {
                await Task.Delay(_options.Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var scope = serviceProvider.CreateScope();

        var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserSearchRepository>();
        var textBuilder = scope.ServiceProvider.GetRequiredService<UserSearchTextBuilder>();

        var seenIds = new HashSet<Guid>();
        var totalUpserts = 0;
        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var page = await directory.ListPageAsync(offset, _options.PageSize, cancellationToken)
                .ConfigureAwait(false);

            if (page.Count == 0)
                break;

            foreach (var item in page)
            {
                seenIds.Add(item.UserId);

                // Пропускаем юзеров без display name: писать в проекцию запись с
                // DisplayName="" вредно — она потом маскирует fallback на KC при
                // повторных запросах. Reconciler догонит их при следующей итерации.
                if (string.IsNullOrWhiteSpace(item.DisplayName))
                    continue;

                var (searchText, translit) = textBuilder.Build(
                    item.Username, item.FirstName, item.LastName, item.Email);

                await repository.UpsertAsync(
                    new UserSearchUpsert(
                        UserId: item.UserId,
                        Username: item.Username,
                        FirstName: item.FirstName,
                        LastName: item.LastName,
                        Email: item.Email,
                        DisplayName: item.DisplayName,
                        SearchText: searchText,
                        SearchTextTranslit: translit,
                        KeycloakModifiedMs: item.ModifiedTimestampMs),
                    cancellationToken).ConfigureAwait(false);

                totalUpserts++;
            }

            if (page.Count < _options.PageSize)
                break;

            offset += page.Count;
        }

        var deleted = await repository.SoftDeleteMissingAsync(seenIds, cancellationToken)
            .ConfigureAwait(false);

        sw.Stop();
        ReconcileCompleted(logger, totalUpserts, deleted, sw.ElapsedMilliseconds, null);
    }

    // LoggerMessage source-gen избегает аллокации params object?[] на горячем пути логгера
    // (требование CA1873 для .NET 10). Reconciler шлёт строку раз в 5 минут, но анализатор
    // не делает исключений — соблюдаем общий стиль.
    private static readonly Action<ILogger, int, int, long, Exception?> ReconcileCompleted =
        LoggerMessage.Define<int, int, long>(
            LogLevel.Information,
            new EventId(1001, nameof(UserSearchReconciler)),
            "UserSearchReconciler: reconciled {Upserts} users, soft-deleted {Deleted}, took {Elapsed}ms.");
}

public sealed class UserSearchReconcilerOptions
{
    public const string SectionName = "UserSearch:Reconciler";

    public bool Enabled { get; set; } = true;
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
    public int PageSize { get; set; } = 200;
}
