using System.Text;
using System.Text.Json;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Cursors;

/// <summary>
/// Thrown when an opaque cursor cannot be parsed. Surfaced as HTTP 400 (Bad Request) by the
/// endpoint layer.
/// </summary>
public sealed class InvalidChatCursorException : ArgumentException
{
    public InvalidChatCursorException()
    {
    }

    public InvalidChatCursorException(string message)
        : base(message)
    {
    }

    public InvalidChatCursorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public InvalidChatCursorException(string message, string? paramName, Exception? innerException)
        : base(message, paramName, innerException)
    {
    }
}

internal static class CursorCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string EncodeConversation(ConversationCursor cursor)
    {
        var payload = new ConversationCursorPayload(cursor.LastMessageAtUtc.ToUnixTimeMilliseconds(), cursor.ConversationId);
        return EncodeBase64Url(JsonSerializer.SerializeToUtf8Bytes(payload, Options));
    }

    public static ConversationCursor? DecodeConversation(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var bytes = DecodeBase64Url(cursor);
            var payload = JsonSerializer.Deserialize<ConversationCursorPayload>(bytes, Options)
                ?? throw new FormatException("Empty cursor payload.");
            return new ConversationCursor(DateTimeOffset.FromUnixTimeMilliseconds(payload.Ts), payload.Id);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new InvalidChatCursorException("Invalid cursor.", nameof(cursor), ex);
        }
    }

    public static string EncodeMessage(MessageCursor cursor)
    {
        var payload = new MessageCursorPayload(cursor.CreatedAtUtc.ToUnixTimeMilliseconds(), cursor.MessageId);
        return EncodeBase64Url(JsonSerializer.SerializeToUtf8Bytes(payload, Options));
    }

    public static MessageCursor? DecodeMessage(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var bytes = DecodeBase64Url(cursor);
            var payload = JsonSerializer.Deserialize<MessageCursorPayload>(bytes, Options)
                ?? throw new FormatException("Empty cursor payload.");
            return new MessageCursor(DateTimeOffset.FromUnixTimeMilliseconds(payload.Ts), payload.Id);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new InvalidChatCursorException("Invalid cursor.", nameof(cursor), ex);
        }
    }

    public static string EncodeThreadActivity(ThreadActivityCursor cursor)
    {
        var payload = new ThreadActivityCursorPayload(cursor.LastActivityAtUtc.ToUnixTimeMilliseconds(), cursor.RootMessageId);
        return EncodeBase64Url(JsonSerializer.SerializeToUtf8Bytes(payload, Options));
    }

    public static ThreadActivityCursor? DecodeThreadActivity(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var bytes = DecodeBase64Url(cursor);
            var payload = JsonSerializer.Deserialize<ThreadActivityCursorPayload>(bytes, Options)
                ?? throw new FormatException("Empty cursor payload.");
            return new ThreadActivityCursor(DateTimeOffset.FromUnixTimeMilliseconds(payload.Ts), payload.Id);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new InvalidChatCursorException("Invalid cursor.", nameof(cursor), ex);
        }
    }

    private static string EncodeBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeBase64Url(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    private sealed record ConversationCursorPayload(long Ts, string Id);
    private sealed record MessageCursorPayload(long Ts, Guid Id);
    private sealed record ThreadActivityCursorPayload(long Ts, Guid Id);
}
