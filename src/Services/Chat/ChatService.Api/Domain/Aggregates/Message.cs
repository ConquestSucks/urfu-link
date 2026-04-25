using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Domain.Aggregates;

public sealed class Message
{
    private readonly List<Attachment> _attachments;

    private Message(
        Guid id,
        string conversationId,
        Guid senderId,
        string body,
        IEnumerable<Attachment> attachments,
        string clientMessageId,
        MessageState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? deliveredAtUtc,
        DateTimeOffset? readAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        SenderId = senderId;
        Body = body;
        _attachments = attachments.ToList();
        ClientMessageId = clientMessageId;
        State = state;
        CreatedAtUtc = createdAtUtc;
        DeliveredAtUtc = deliveredAtUtc;
        ReadAtUtc = readAtUtc;
    }

    public Guid Id { get; }

    public string ConversationId { get; }

    public Guid SenderId { get; }

    public string Body { get; }

    public IReadOnlyList<Attachment> Attachments => _attachments;

    public string ClientMessageId { get; }

    public MessageState State { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool HasAttachments => _attachments.Count > 0;

    public static Message Send(
        Guid id,
        string conversationId,
        Guid senderId,
        string body,
        IEnumerable<Attachment> attachments,
        string clientMessageId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientMessageId);

        return new Message(
            id,
            conversationId,
            senderId,
            body ?? string.Empty,
            attachments,
            clientMessageId,
            MessageState.Sent,
            createdAtUtc,
            deliveredAtUtc: null,
            readAtUtc: null);
    }

    public static Message Hydrate(
        Guid id,
        string conversationId,
        Guid senderId,
        string body,
        IEnumerable<Attachment> attachments,
        string clientMessageId,
        MessageState state,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? deliveredAtUtc,
        DateTimeOffset? readAtUtc)
        => new(id, conversationId, senderId, body, attachments, clientMessageId, state, createdAtUtc, deliveredAtUtc, readAtUtc);

    public bool MarkDelivered(DateTimeOffset atUtc)
    {
        if (State != MessageState.Sent)
        {
            return false;
        }

        State = MessageState.Delivered;
        DeliveredAtUtc = atUtc;
        return true;
    }

    public bool MarkRead(DateTimeOffset atUtc)
    {
        if (State == MessageState.Read || State == MessageState.Deleted)
        {
            return false;
        }

        DeliveredAtUtc ??= atUtc;
        ReadAtUtc = atUtc;
        State = MessageState.Read;
        return true;
    }
}
