import { useMemo } from "react";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import type { InboxChatProps } from "@/entities/inbox-chat";
import type { ConversationPreview } from "@urfu-link/api-client";

/**
 * Маппинг ConversationPreview → InboxChatProps. До backend-обогащения DTO
 * (см. follow-up "Enrich ConversationPreview with avatarUrl, title, unreadCount")
 * часть полей берётся с fallback'ами.
 */
const mapConversation = (
    c: ConversationPreview,
    currentUserId: string | null,
): InboxChatProps => ({
    id: c.id,
    avatarUrl: "",
    name:
        c.title ??
        (c.type === "Direct" ? "Личный чат" : "Чат предмета"),
    message: c.lastMessagePreview?.body ?? "",
    time: c.lastMessageAt
        ? new Date(c.lastMessageAt).toLocaleTimeString([], {
              hour: "2-digit",
              minute: "2-digit",
          })
        : "",
    unreadCount: 0,
    lastMessageFromSelf:
        !!currentUserId && c.lastMessagePreview?.senderId === currentUserId,
    lastMessageRead: true,
});

/**
 * Селектор: список conversations нужного типа, замаппленный в InboxChatProps.
 * Тип "chats" → Direct, "subjects" → Discipline.
 *
 * Источник данных — useChatStore.conversations, который обновляется
 * в реальном времени через SignalR ConversationUpdated.
 */
export const useInboxConversations = (
    tab: "chats" | "subjects",
): InboxChatProps[] => {
    const conversations = useChatStore((s) => s.conversations);
    const currentUserId = useCurrentUserId();
    const wantedType = tab === "chats" ? "Direct" : "Discipline";

    return useMemo(
        () =>
            conversations
                .filter((c) => c.type === wantedType)
                .map((c) => mapConversation(c, currentUserId)),
        [conversations, currentUserId, wantedType],
    );
};
