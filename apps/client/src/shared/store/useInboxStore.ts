import { InboxNotificationProps } from "@/entities/inbox-notification";
import { inboxApi } from "@/shared/api";
import { useNotificationStore } from "@/shared/store/notification-store";
import { create } from "zustand";
import { useShallow } from "zustand/react/shallow";

/**
 * Inbox-store держит только UI-state (мобильные табы) и notifications.
 * Список чатов/предметов перенесён в useChatStore.conversations (single source
 * of truth, обновляется в реальном времени через SignalR). См.
 * widgets/inbox/model/use-inbox-conversations.ts для маппинга в InboxChatProps.
 */
interface InboxState {
    notifications: InboxNotificationProps[];
    isNotificationsLoading: boolean;
    isMarkingAllNotificationsRead: boolean;
    mobileInboxTab: "chats" | "subjects";
    setMobileInboxTab: (tab: "chats" | "subjects") => void;
    mobileInboxListMode: "messages" | "notifications";
    setMobileInboxListMode: (mode: "messages" | "notifications") => void;
    fetchNotifications: () => Promise<void>;
    markNotificationRead: (id: string) => Promise<void>;
    markAllNotificationsRead: (ids?: string[]) => Promise<void>;
}

const refreshNotificationBadge = async () => {
    try {
        const badge = await inboxApi.getNotificationBadge();
        useNotificationStore.getState().setBadge(badge);
    } catch {
        // Badge reconciliation is best-effort; the next poll/SignalR update will fix it.
    }
};

export const useInboxStore = create<InboxState>((set, get) => ({
    notifications: [],
    isNotificationsLoading: false,
    isMarkingAllNotificationsRead: false,

    mobileInboxTab: "chats",
    setMobileInboxTab: (tab) => set({ mobileInboxTab: tab }),

    mobileInboxListMode: "messages",
    setMobileInboxListMode: (mode) => set({ mobileInboxListMode: mode }),

    fetchNotifications: async () => {
        if (get().notifications.length > 0) return;
        set({ isNotificationsLoading: true });
        try {
            const data = await inboxApi.getNotifications();
            set({ notifications: data, isNotificationsLoading: false });
        } catch {
            set({ isNotificationsLoading: false });
        }
    },

    markNotificationRead: async (id) => {
        const notifications = get().notifications;
        const notification = notifications.find((item) => item.id === id);

        if (!notification || notification.isRead !== false) return;

        set({
            notifications: notifications.map((item) =>
                item.id === id ? { ...item, isRead: true } : item,
            ),
        });

        try {
            await inboxApi.markNotificationRead(id);
            await refreshNotificationBadge();
        } catch {
            set({ notifications });
        }
    },

    markAllNotificationsRead: async (ids) => {
        const notifications = get().notifications;
        const unreadIds = ids
            ? ids.filter((id) =>
                  notifications.some((item) => item.id === id && item.isRead === false),
              )
            : notifications.filter((item) => item.isRead === false).map((item) => item.id);

        if (unreadIds.length === 0) return;

        const unreadIdSet = new Set(unreadIds);

        set({
            isMarkingAllNotificationsRead: true,
            notifications: notifications.map((item) =>
                unreadIdSet.has(item.id) ? { ...item, isRead: true } : item,
            ),
        });

        try {
            await inboxApi.markNotificationsRead(unreadIds);
            await refreshNotificationBadge();
            set({ isMarkingAllNotificationsRead: false });
        } catch {
            set({
                notifications,
                isMarkingAllNotificationsRead: false,
            });
        }
    },
}));

export const useInboxMobileState = () =>
    useInboxStore(
        useShallow((state) => ({
            mobileInboxTab: state.mobileInboxTab,
            setMobileInboxTab: state.setMobileInboxTab,
            mobileInboxListMode: state.mobileInboxListMode,
            setMobileInboxListMode: state.setMobileInboxListMode,
        })),
    );
