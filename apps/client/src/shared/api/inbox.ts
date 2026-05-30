import { InboxNotificationProps } from "@/entities/inbox-notification";
import { apiClient } from "@/shared/lib/api";
import { inferNotificationScope } from "@/shared/lib/notificationDeepLink";

const formatTime = (iso: string) =>
    new Intl.DateTimeFormat("ru-RU", {
        day: "2-digit",
        month: "short",
        hour: "2-digit",
        minute: "2-digit",
    }).format(new Date(iso));

/**
 * Inbox API сжалось до notifications: чаты и предметы теперь читаются
 * из useChatStore.conversations через widgets/inbox/model/use-inbox-conversations.ts.
 */
export const inboxApi = {
    getNotifications: async (): Promise<InboxNotificationProps[]> => {
        const response = await apiClient.notifications.list({ limit: 50, status: "all" });
        return response.items.map((item) => {
            const deepLink =
                item.deepLink ??
                item.actions.find((action) => action.deepLink)?.deepLink ??
                null;

            return {
                id: item.id,
                title: item.title,
                description: item.body,
                time: formatTime(item.lastOccurrenceAtUtc),
                scope: inferNotificationScope({ ...item, deepLink }),
                deepLink,
                data: item.data,
                actorName: item.actor?.displayName ?? null,
                isRead: item.readAtUtc !== null,
            };
        });
    },

    markNotificationRead: (id: string): Promise<void> =>
        apiClient.notifications.markRead(id),

    markNotificationsRead: (ids: string[]): Promise<{ updated: number }> =>
        apiClient.notifications.bulk({
            action: "read",
            ids,
        }),

    getNotificationBadge: () => apiClient.notifications.getBadge(),
};
