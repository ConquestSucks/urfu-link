using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

/// <summary>
/// Singleton wrapper around <see cref="IMongoDatabase"/> exposing typed collections for chat
/// aggregates. The underlying <see cref="MongoClient"/> is itself thread-safe, so the context
/// holds a single shared instance per process.
/// </summary>
public sealed class ChatMongoContext : IDisposable
{
    public const string ConversationsCollectionName = "conversations";
    public const string MessagesCollectionName = "messages";

    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;

    public ChatMongoContext(IOptions<ChatMongoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        BsonRegistrations.EnsureRegistered();

        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            throw new InvalidOperationException("ChatMongo connection string is not configured.");
        }

        var url = MongoUrl.Create(opts.ConnectionString);
        _client = new MongoClient(url);
        var databaseName = !string.IsNullOrWhiteSpace(opts.DatabaseName)
            ? opts.DatabaseName
            : url.DatabaseName ?? "chat_db";
        _database = _client.GetDatabase(databaseName);
    }

    public IMongoDatabase Database => _database;

    public IMongoClient Client => _client;

    public void Dispose() => _client.Dispose();

    internal IMongoCollection<ConversationDocument> Conversations
        => _database.GetCollection<ConversationDocument>(ConversationsCollectionName);

    internal IMongoCollection<MessageDocument> Messages
        => _database.GetCollection<MessageDocument>(MessagesCollectionName);
}
