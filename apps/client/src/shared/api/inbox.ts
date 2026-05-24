import { InboxNotificationProps } from "@/entities/inbox-notification";

/**
 * Inbox API сжалось до notifications: чаты и предметы теперь читаются
 * из useChatStore.conversations через widgets/inbox/model/use-inbox-conversations.ts.
 */
export const inboxApi = {
    getNotifications: async (): Promise<InboxNotificationProps[]> => {
        // TODO: подключить реальный NotificationService.
        return [];
    },
};
