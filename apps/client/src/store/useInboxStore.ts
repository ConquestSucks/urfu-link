import { InboxChatProps } from "@/entities/inbox-chat";
import { InboxNotificationProps } from "@/entities/inbox-notification";
import { InboxSubjectProps } from "@/entities/inbox-subject";
import { TabType } from "@/entities/tab";
import { ViewType } from "@/entities/view";
import { inboxApi } from "@/shared/api";
import { create } from "zustand";
import { useShallow } from "zustand/react/shallow";
interface InboxState {
    chats: InboxChatProps[];
    subjects: InboxSubjectProps[];
    notifications: InboxNotificationProps[];
    isChatsLoading: boolean;
    isSubjectsLoading: boolean;
    isNotificationsLoading: boolean;
    mobileInboxTab: "chats" | "subjects";
    setMobileInboxTab: (tab: "chats" | "subjects") => void;
    mobileInboxListMode: "messages" | "notifications";
    setMobileInboxListMode: (mode: "messages" | "notifications") => void;
    getChatById: (id: string) => InboxChatProps | undefined;
    getSubjectMessageById: (id: string) => InboxChatProps | undefined;
    fetchChats: () => Promise<void>;
    fetchSubjects: () => Promise<void>;
    fetchNotifications: () => Promise<void>;
}

export const useInboxStore = create<InboxState>((set, get) => ({
    chats: [],
    subjects: [],
    notifications: [],
    isChatsLoading: false,
    isSubjectsLoading: false,
    isNotificationsLoading: false,

    mobileInboxTab: "chats",
    setMobileInboxTab: (tab) => set({ mobileInboxTab: tab }),

    mobileInboxListMode: "messages",
    setMobileInboxListMode: (mode) => set({ mobileInboxListMode: mode }),

    getChatById: (id: string) => {
        const { chats } = get();
        return chats.find((chat) => chat.id === id);
    },

    getSubjectMessageById: (id: string) => {
        const { subjects } = get();
        for (const subject of subjects) {
            const foundMessage = subject.messages.find((msg) => msg.id === id);
            if (foundMessage) return foundMessage;
        }
        return undefined;
    },

    fetchChats: async () => {
        if (get().chats.length > 0) return;
        set({ isChatsLoading: true });
        try {
            const data = await inboxApi.getChats();
            set({ chats: data, isChatsLoading: false });
        } catch (error) {
            console.error("Ошибка при загрузке чатов:", error);
            set({ isChatsLoading: false });
        }
    },

    fetchSubjects: async () => {
        if (get().subjects.length > 0) return;
        set({ isSubjectsLoading: true });
        try {
            const data = await inboxApi.getSubjects();
            set({ subjects: data, isSubjectsLoading: false });
        } catch (error) {
            console.error("Ошибка при загрузке дисциплин:", error);
            set({ isSubjectsLoading: false });
        }
    },

    fetchNotifications: async () => {
        if (get().notifications.length > 0) return;
        set({ isNotificationsLoading: true });
        try {
            const data = await inboxApi.getNotifications();
            set({ notifications: data, isNotificationsLoading: false });
        } catch (error) {
            console.error("Ошибка при загрузке уведомлений:", error);
            set({ isNotificationsLoading: false });
        }
    },
}));

export const useInboxData = () =>
    useInboxStore(
        useShallow((state) => ({
            chats: state.chats,
            subjects: state.subjects,
            notifications: state.notifications,
            isChatsLoading: state.isChatsLoading,
            isSubjectsLoading: state.isSubjectsLoading,
            isNotificationsLoading: state.isNotificationsLoading,
        })),
    );

export const useInboxMobileState = () =>
    useInboxStore(
        useShallow((state) => ({
            mobileInboxTab: state.mobileInboxTab,
            setMobileInboxTab: state.setMobileInboxTab,
            mobileInboxListMode: state.mobileInboxListMode,
            setMobileInboxListMode: state.setMobileInboxListMode,
        })),
    );

export const useInboxActions = () =>
    useInboxStore(
        useShallow((state) => ({
            fetchChats: state.fetchChats,
            fetchSubjects: state.fetchSubjects,
            fetchNotifications: state.fetchNotifications,
            getChatById: state.getChatById,
            getSubjectMessageById: state.getSubjectMessageById,
        })),
    );
