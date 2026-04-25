using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record ReadReceiptDto(
    Guid UserId,
    DateTimeOffset ReadAtUtc)
{
    public static ReadReceiptDto FromDomain(ReadReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new ReadReceiptDto(receipt.UserId, receipt.ReadAtUtc);
    }
}
