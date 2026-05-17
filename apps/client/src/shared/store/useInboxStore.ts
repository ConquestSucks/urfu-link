import { InboxNotificationProps } from "@/entities/inbox-notification";
import { inboxApi } from "@/shared/api";
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
    mobileInboxTab: "chats" | "subjects";
    setMobileInboxTab: (tab: "chats" | "subjects") => void;
    mobileInboxListMode: "messages" | "notifications";
    setMobileInboxListMode: (mode: "messages" | "notifications") => void;
    fetchNotifications: () => Promise<void>;
}

export const useInboxStore = create<InboxState>((set, get) => ({
    notifications: [],
    isNotificationsLoading: false,

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
