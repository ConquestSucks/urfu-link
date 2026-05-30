using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisciplineChatE2ETests.Infrastructure;

internal static class E2EJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static Task<T?> ReadFromE2EJsonAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.ReadFromJsonAsync<T>(Options, cancellationToken);
    }

    public static Task<T?> GetFromE2EJsonAsync<T>(
        this HttpClient client,
        string? requestUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.GetFromJsonAsync<T>(requestUri, Options, cancellationToken);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
