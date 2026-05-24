using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatService.IntegrationTests.Infrastructure;

internal static class ChatJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static Task<T?> ReadFromChatJsonAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.ReadFromJsonAsync<T>(Options, cancellationToken);
    }

    public static Task<T?> GetFromChatJsonAsync<T>(
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
