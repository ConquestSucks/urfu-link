using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

namespace Urfu.Link.BuildingBlocks.UnitTests.ServiceDefaults;

/// <summary>
/// <see cref="MigrationCliRunner"/> is the shared <c>--migrate</c> entry point for every
/// EF-backed service. The Helm <c>migrations</c> Job spawns a container with this argument
/// before the main app rolls out, so the helper must (a) honour the flag, (b) leave normal
/// startup untouched when it's absent, and (c) surface a non-zero exit code when the
/// migration itself fails so Helm aborts the upgrade.
/// </summary>
public sealed class MigrationCliRunnerTests : IDisposable
{
    private readonly int _originalExitCode;

    public MigrationCliRunnerTests()
    {
        _originalExitCode = Environment.ExitCode;
        Environment.ExitCode = 0;
    }

    public void Dispose()
    {
        Environment.ExitCode = _originalExitCode;
    }

    [Fact]
    public async Task Returns_false_and_skips_migration_when_flag_absent()
    {
        var args = new[] { "--other-flag" };

        var migrated = await MigrationCliRunner.TryRunMigrationsAsync<UnreachableDbContext>(
            args,
            "test-service",
            ConfigureUnreachableDbContext,
            CancellationToken.None);

        migrated.Should().BeFalse("the caller must continue to normal app startup when --migrate isn't passed.");
        Environment.ExitCode.Should().Be(0);
    }

    [Theory]
    [InlineData("--migrate")]
    [InlineData("--MIGRATE")]
    [InlineData("--Migrate")]
    public async Task Recognises_flag_case_insensitively(string flag)
    {
        var args = new[] { flag };

        var migrated = await MigrationCliRunner.TryRunMigrationsAsync<UnreachableDbContext>(
            args,
            "test-service",
            ConfigureUnreachableDbContext,
            CancellationToken.None);

        // Migration mode was triggered (so caller should exit), even though the migration itself failed.
        migrated.Should().BeTrue();
    }

    [Fact]
    public async Task Sets_exit_code_to_one_when_migration_fails()
    {
        var args = new[] { "--migrate" };

        var migrated = await MigrationCliRunner.TryRunMigrationsAsync<UnreachableDbContext>(
            args,
            "test-service",
            ConfigureUnreachableDbContext,
            CancellationToken.None);

        migrated.Should().BeTrue();
        Environment.ExitCode.Should().Be(1, "non-zero exit fails the Helm pre-upgrade Job and aborts the rollout.");
    }

    private static void ConfigureUnreachableDbContext(IServiceCollection services)
    {
        // Postgres at port 1 is guaranteed to refuse the connection — exercises the failure path
        // without binding the test to a real container.
        services.AddDbContext<UnreachableDbContext>(options =>
            options.UseNpgsql("Host=localhost;Port=1;Database=missing;Username=u;Password=p;Timeout=2;Command Timeout=2"));
    }

    private sealed class UnreachableDbContext(DbContextOptions<UnreachableDbContext> options) : DbContext(options);
}
