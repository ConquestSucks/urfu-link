using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace MediaService.IntegrationTests.Infrastructure;

public class StorageOptionsValidationTests
{
    [Fact]
    public void MissingAccessKey_FailsStartup()
    {
        using var baseFactory = new MediaServiceFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:AccessKey"] = string.Empty,
                });
            });
        });

        var act = () => _ = factory.Services;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AccessKey*");
    }
}
