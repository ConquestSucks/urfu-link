using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Application.Services;

internal sealed record BadgeScopeInput(NotificationCategory Category, string? ConversationId);

internal static class BadgeScopeClassifier
{
    private const string DisciplineConversationPrefix = "discipline:";

    private static readonly HashSet<NotificationCategory> DisciplineCategories =
    [
        NotificationCategory.ChatMessageDiscipline,
        NotificationCategory.DisciplineAnnouncement,
        NotificationCategory.DisciplineMaterial,
        NotificationCategory.DisciplineDeadline,
        NotificationCategory.DisciplineEnrollment,
        NotificationCategory.DisciplineUnenrollment,
        NotificationCategory.DisciplineUpdated,
        NotificationCategory.DisciplineDeleted,
    ];

    public static IReadOnlyDictionary<string, int> ClassifyUnreadScopes(
        IReadOnlyList<BadgeScopeInput> inputs,
        IReadOnlySet<string> disciplineConversationIds)
    {
        var chats = 0;
        var disciplines = 0;

        foreach (var input in inputs)
        {
            if (DisciplineCategories.Contains(input.Category))
            {
                disciplines++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(input.ConversationId) &&
                (disciplineConversationIds.Contains(input.ConversationId) ||
                 input.ConversationId.StartsWith(DisciplineConversationPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                disciplines++;
                continue;
            }

            chats++;
        }

        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["chats"] = chats,
            ["disciplines"] = disciplines,
        };
    }
}
