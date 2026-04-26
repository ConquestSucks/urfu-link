namespace Urfu.Link.Services.Chat.Application;

/// <summary>
/// Shared payload constraints for chat messages. Applied by <c>SendMessageService</c> on send
/// and by <c>EditMessageService</c> on edit so both paths reject the same outliers.
/// </summary>
public static class ChatBodyConstraints
{
    public const int MaxBodyLength = 4000;

    public const int MaxAttachmentsPerMessage = 10;
}
