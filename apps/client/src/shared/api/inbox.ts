import { InboxNotificationProps } from "@/entities/inbox-notification";
import { apiClient } from "@/shared/lib/api";

const chatCategories = new Set([1, 2, 10, 11, 40, 41]);

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
        return response.items.map((item) => ({
            id: item.id,
            title: item.title,
            description: item.body,
            time: formatTime(item.lastOccurrenceAtUtc),
            scope: chatCategories.has(item.category) ? "chats" : "subjects",
            isRead: item.readAtUtc !== null,
        }));
    },
};
