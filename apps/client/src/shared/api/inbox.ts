import { InboxChatProps } from "@/entities/inbox-chat/model/types";
import { InboxSubjectProps } from "@/entities/inbox-subject";
import { apiClient } from "@/shared/lib/api";
import { ConversationPreview } from "@urfu-link/api-client";
import { useAuthStore } from "@/shared/store/auth-store";

const mapConversation = (c: ConversationPreview): InboxChatProps => {
    // Basic mapping, needs refinement for real user names/avatars
    const currentUserId = useAuthStore.getState().accessToken ? "me" : null;
    
    return {
        id: c.id,
        avatarUrl: "", // Need user avatar
        name: c.type === "Direct" ? "Direct Chat" : "Discipline Chat", // Need real peer/discipline name
        message: c.lastMessagePreview?.body || "",
        time: c.lastMessageAt ? new Date(c.lastMessageAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : "",
        unreadCount: 0, // Not provided by preview yet
        lastMessageFromSelf: c.lastMessagePreview?.senderId === currentUserId,
        lastMessageRead: true // Need real read status
    };
};

export const inboxApi = {
    getChats: async (): Promise<InboxChatProps[]> => {
        try {
            const res = await apiClient.chat.getConversations("Direct", undefined, 50);
            return res.items.map(mapConversation);
        } catch (error) {
            console.error("Failed to fetch direct chats", error);
            return [];
        }
    },
    getSubjects: async (): Promise<InboxSubjectProps[]> => {
        try {
            const res = await apiClient.chat.getConversations("Discipline", undefined, 50);
            return res.items.map(c => ({
                id: c.id,
                title: "Discipline", // Need real discipline title
                messages: [mapConversation(c)]
            }));
        } catch (error) {
            console.error("Failed to fetch subjects", error);
            return [];
        }
    },
    getNotifications: async () => {
        // Still needs mock or actual implementation
        return [];
    },
    getChatMeta: async (id: string, type: "chat" | "subject"): Promise<InboxChatProps | undefined> => {
        try {
            const res = await apiClient.chat.getConversation(id);
            return mapConversation(res);
        } catch (error) {
            console.error("Failed to fetch chat meta", error);
            return undefined;
        }
    },
};
