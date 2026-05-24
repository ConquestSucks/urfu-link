using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Application.Contracts;

public sealed record EditHistoryEntryDto(
    string Body,
    DateTimeOffset EditedAtUtc)
{
    public static EditHistoryEntryDto FromDomain(EditHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new EditHistoryEntryDto(entry.Body, entry.EditedAtUtc);
    }
}
