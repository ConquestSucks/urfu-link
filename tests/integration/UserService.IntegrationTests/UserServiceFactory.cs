using Amazon.S3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Persistence;
using UserService.IntegrationTests.Helpers;

namespace UserService.IntegrationTests;

public sealed class UserServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            // Replace EF Core with in-memory database
            var dbDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("UserDbContext", StringComparison.Ordinal) == true
                    || d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) == true
                    || d.ServiceType.FullName?.Contains("IDbContextPool", StringComparison.Ordinal) == true
                    || d.ServiceType.FullName?.Contains("IScopedDbContextLease", StringComparison.Ordinal) == true)
                .ToList();
            foreach (var descriptor in dbDescriptors)
            {
                services.Remove(descriptor);
            }

            var dbName = "TestDb_" + Guid.NewGuid().ToString("N");
            services.AddDbContext<UserDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // Replace Redis/Outbox with fakes
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<IOutboxWriter>();
            services.AddSingleton<IOutboxWriter, FakeOutboxWriter>();

            // Replace S3 client with mock
            services.RemoveAll<IAmazonS3>();
            services.AddSingleton(Substitute.For<IAmazonS3>());

            // Replace external infrastructure with fakes
            services.RemoveAll<IAvatarStorage>();
            services.AddSingleton<IAvatarStorage, FakeAvatarStorage>();

            services.RemoveAll<ISessionManager>();
            services.AddSingleton<ISessionManager, FakeSessionManager>();

            services.RemoveAll<ISessionRevocationStore>();
            services.AddSingleton(Substitute.For<ISessionRevocationStore>());

            // Replace authentication with test scheme
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
}
