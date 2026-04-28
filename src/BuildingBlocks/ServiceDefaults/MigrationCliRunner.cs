using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Urfu.Link.BuildingBlocks.ServiceDefaults;

/// <summary>
/// Centralised <c>--migrate</c> entry point for every EF-backed service.
/// </summary>
/// <remarks>
/// In production the Helm <c>migrations</c> Job (a <c>pre-install,pre-upgrade</c> hook with
/// <c>helm.sh/hook-weight: -5</c>) spawns a one-shot container with the same image, the same
/// secrets, and a single argument: <c>--migrate</c>. The Job blocks the rollout until it
/// finishes — a non-zero exit aborts the upgrade so the new app pods never start against an
/// un-migrated schema. <see cref="MigrationCliRunner"/> is the single piece of code that
/// implements that contract; calling it from <c>Program.cs</c> before <c>WebApplication.CreateBuilder</c>
/// keeps the normal startup path free of any auto-migration hazards (race between replicas,
/// silent schema upgrades on every pod restart, etc.).
/// </remarks>
public static class MigrationCliRunner
{
    /// <summary>The CLI flag that selects migration mode.</summary>
    public const string MigrateArg = "--migrate";

    /// <summary>
    /// Inspects <paramref name="args"/> for <see cref="MigrateArg"/>; if present, materialises
    /// <typeparamref name="TContext"/> via <paramref name="configureServices"/> and applies all
    /// pending migrations. Returns <see langword="true"/> when migration mode was triggered so
    /// the caller can <c>return</c> from <c>Program.cs</c> instead of starting the web host.
    /// On migration failure <see cref="Environment.ExitCode"/> is set to <c>1</c> so the
    /// container exits non-zero and Helm aborts the rollout.
    /// </summary>
    public static async Task<bool> TryRunMigrationsAsync<TContext>(
        string[] args,
        string serviceName,
        Action<IServiceCollection> configureServices,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(configureServices);

        if (!args.Any(a => string.Equals(a, MigrateArg, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            var services = new ServiceCollection();
            configureServices(services);

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();

            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"{serviceName} migrations applied successfully.").ConfigureAwait(false);
        }
#pragma warning disable CA1031 // CLI entry-point must catch any failure to surface a non-zero exit code for Helm.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await Console.Error.WriteLineAsync(
                $"{serviceName} migration failed: {ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);
            await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
            Environment.ExitCode = 1;
        }

        return true;
    }
}
