import { useEffect, useMemo } from "react";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useParticipantsStore } from "@/entities/conversation/model/participants-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import type { InboxChatProps } from "@/entities/inbox-chat";
import type { ConversationPreview } from "@urfu-link/api-client";

/**
 * Маппинг ConversationPreview → InboxChatProps. До backend-обогащения DTO
 * (см. follow-up "Enrich ConversationPreview with avatarUrl, title, unreadCount")
 * имя и аватар direct-чата вычисляем по собеседнику из participants-кэша.
 */
const mapConversation = (
    c: ConversationPreview,
    currentUserId: string | null,
    peerName: string | null,
    peerAvatar: string | null,
): InboxChatProps => {
    const lastAt = c.lastMessageAtUtc ?? c.lastMessageAt ?? null;
    return {
        id: c.id,
        avatarUrl: peerAvatar ?? "",
        name:
            c.title ??
            peerName ??
            (c.type === "Direct" ? "Личный чат" : "Чат предмета"),
        message: c.lastMessagePreview?.body ?? "",
        time: lastAt
            ? new Date(lastAt).toLocaleTimeString([], {
                  hour: "2-digit",
                  minute: "2-digit",
              })
            : "",
        unreadCount: 0,
        lastMessageFromSelf:
            !!currentUserId && c.lastMessagePreview?.senderId === currentUserId,
        lastMessageRead: true,
    };
};

/**
 * Селектор: список conversations нужного типа, замаппленный в InboxChatProps.
 * Тип "chats" → Direct, "subjects" → Discipline.
 *
 * Источник данных — useChatStore.conversations, который обновляется
 * в реальном времени через SignalR ConversationUpdated. Для direct-чатов
 * дополнительно прогреваем participants-кэш (TTL 5 мин в participants-store),
 * чтобы в списке отображалось имя собеседника, а не "Личный чат".
 */
export const useInboxConversations = (
    tab: "chats" | "subjects",
): InboxChatProps[] => {
    const conversations = useChatStore((s) => s.conversations);
    const participantsByConversation = useParticipantsStore(
        (s) => s.byConversationId,
    );
    const currentUserId = useCurrentUserId();

    // "chats" → Direct, "subjects" → Group с groupSubtype === "Discipline".
    // ChatService.Domain различает Direct (Type=0) и Group (Type=1) с подтипом.
    const filtered = useMemo(
        () =>
            conversations.filter((c) =>
                tab === "chats"
                    ? c.type === "Direct"
                    : c.type === "Group" && c.groupSubtype === "Discipline",
            ),
        [conversations, tab],
    );

    // Прогреваем participants для всех direct-чатов в списке. participants-store
    // сам дедуплицирует параллельные запросы по conversationId и кэширует на 5 мин.
    useEffect(() => {
        if (tab !== "chats") return;
        const load = useParticipantsStore.getState().load;
        for (const c of filtered) {
            if (!participantsByConversation[c.id]) {
                load(c.id).catch(() => {
                    /* fail-open: останется "Личный чат" до retry */
                });
            }
        }
    }, [filtered, tab, participantsByConversation]);

    return useMemo(
        () =>
            filtered.map((c) => {
                if (c.type !== "Direct") {
                    return mapConversation(c, currentUserId, null, null);
                }
                const items = participantsByConversation[c.id]?.items;
                const peer = items?.find((p) => p.userId !== currentUserId)
                    ?? items?.[0];
                // Бэк fail-open отдаёт пустую строку при недоступном UserService;
                // нормализуем в null, чтобы сработал fallback "Личный чат" в mapConversation.
                const peerName = peer?.displayName?.trim() ? peer.displayName : null;
                const peerAvatar = peer?.avatarUrl?.trim() ? peer.avatarUrl : null;
                return mapConversation(c, currentUserId, peerName, peerAvatar);
            }),
        [filtered, currentUserId, participantsByConversation],
    );
};
