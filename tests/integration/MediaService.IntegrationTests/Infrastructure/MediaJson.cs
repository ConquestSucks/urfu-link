using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaService.IntegrationTests.Infrastructure;

internal static class MediaJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static Task<T?> ReadAsync<T>(HttpContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return content.ReadFromJsonAsync<T>(Options, cancellationToken);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
