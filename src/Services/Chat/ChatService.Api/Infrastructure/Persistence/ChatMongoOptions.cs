namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

public sealed class ChatMongoOptions
{
    public const string SectionName = "ChatMongo";

    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "chat_db";
}
