import { useEffect, useMemo } from "react";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useParticipantsStore } from "@/entities/conversation/model/participants-store";
import {
    toPresenceTypingConversationId,
    usePresenceStore,
} from "@/entities/presence/model/presence-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import type { InboxChatProps } from "@/entities/inbox-chat";
import type { AttachmentType, ConversationPreview, MessageDto } from "@urfu-link/api-client";

const formatInboxTime = (value: string | null | undefined) => {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    return date.toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
    });
};

type InboxTypingUser = {
    userId: string;
    displayName?: string;
};

const formatTypingPreview = (
    typers: InboxTypingUser[],
    showNames: boolean,
): string | null => {
    if (typers.length === 0) return null;
    if (!showNames) return "Печатает";

    if (typers.length === 1) {
        const displayName = typers[0].displayName?.trim();
        return displayName ? `${displayName} печатает` : "Печатает";
    }

    const lastDigit = typers.length % 10;
    const lastTwoDigits = typers.length % 100;
    const peopleWord =
        lastDigit >= 2 && lastDigit <= 4 && (lastTwoDigits < 12 || lastTwoDigits > 14)
            ? "человека"
            : "человек";

    return `${typers.length} ${peopleWord} печатают`;
};

const fileWord = (count: number) => {
    const lastDigit = count % 10;
    const lastTwoDigits = count % 100;
    if (lastDigit === 1 && lastTwoDigits !== 11) return "файл";
    if (
        lastDigit >= 2 &&
        lastDigit <= 4 &&
        (lastTwoDigits < 12 || lastTwoDigits > 14)
    ) {
        return "файла";
    }
    return "файлов";
};

const formatFileCount = (count: number) => `${count} ${fileWord(count)}`;

const formatAttachmentPreview = (
    fileNames: string[] | undefined,
    hasAttachments: boolean | undefined,
    attachmentTypes?: AttachmentType[],
) => {
    if (attachmentTypes?.includes("Voice")) return "Голосовое сообщение";

    const names = (fileNames ?? [])
        .map((name) => name.trim())
        .filter((name) => name.length > 0);

    if (names.length === 1) return names[0];
    if (names.length > 1) {
        return `${names[0]} и еще ${formatFileCount(names.length - 1)}`;
    }

    return hasAttachments ? "Файл" : "";
};

const formatLastMessagePreview = (
    latestMessage: MessageDto | undefined,
    preview: ConversationPreview["lastMessagePreview"],
) => {
    if (latestMessage) {
        const body = latestMessage.body.trim();
        if (body.length > 0) return latestMessage.body;
        const attachments = latestMessage.attachments ?? [];

        return formatAttachmentPreview(
            attachments.map((a) => a.fileName),
            attachments.length > 0,
            attachments.map((a) => a.type),
        );
    }

    if (!preview) return "";

    const body = preview.body.trim();
    if (body.length > 0) return preview.body;

    return formatAttachmentPreview(
        preview.attachmentFileNames,
        preview.hasAttachments,
        preview.attachmentTypes,
    );
};

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
    latestMessage: MessageDto | undefined,
    unreadCount: number,
    typingPreview: string | null,
): InboxChatProps => {
    const preview = c.lastMessagePreview;
    const senderId = latestMessage?.senderId ?? preview?.senderId;
    const previewReadAt = preview?.readAtUtc ?? preview?.readAt ?? null;
    const lastAt =
        latestMessage?.createdAt ??
        preview?.sentAtUtc ??
        preview?.sentAt ??
        (preview ? c.lastMessageAtUtc ?? c.lastMessageAt ?? null : null);
    const isDisciplineGroup = c.type === "Group" && c.groupSubtype === "Discipline";
    const disciplineRowName = isDisciplineGroup
        ? c.disciplineChatKind === "Subgroup"
            ? c.disciplineSubgroupName ?? c.title ?? "Подгруппа"
            : "Общий чат"
        : null;
    return {
        id: c.id,
        avatarUrl: peerAvatar ?? "",
        name:
            disciplineRowName ??
            c.title ??
            peerName ??
            (c.type === "Direct" ? "Личный чат" : "Чат дисциплины"),
        message: typingPreview ?? formatLastMessagePreview(latestMessage, preview),
        time: formatInboxTime(lastAt),
        unreadCount,
        lastMessageFromSelf: !typingPreview && !!currentUserId && senderId === currentUserId,
        lastMessageRead: !typingPreview
            ? latestMessage
                ? latestMessage.readAt !== null
                : previewReadAt !== null
            : false,
        isTyping: typingPreview !== null,
        disciplineId: c.disciplineId ?? null,
        disciplineTitle: c.disciplineTitle ?? c.title ?? null,
        disciplineChatKind: c.disciplineChatKind ?? null,
        disciplineSubgroupName: c.disciplineSubgroupName ?? null,
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
    const messagesByConversation = useChatStore((s) => s.messagesByConversation);
    const unreadByConversation = useChatStore((s) => s.unreadByConversation);
    const typingByConversation = usePresenceStore((s) => s.typingByConversation);
    const watchUserPresence = usePresenceStore((s) => s.watchUserPresence);
    const unwatchUserPresence = usePresenceStore((s) => s.unwatchUserPresence);
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
                    ? c.type === "Direct" &&
                      (c.lastMessagePreview !== null ||
                          (messagesByConversation[c.id]?.length ?? 0) > 0)
                    : c.type === "Group" && c.groupSubtype === "Discipline",
            ),
        [conversations, messagesByConversation, tab],
    );

    const directPeerUserIds = useMemo(() => {
        if (tab !== "chats") return [];

        const ids = new Set<string>();
        for (const c of filtered) {
            if (c.type !== "Direct") continue;

            const items = participantsByConversation[c.id]?.items;
            const peer = items?.find((p) => p.userId !== currentUserId) ?? items?.[0];
            if (peer?.userId && peer.userId !== currentUserId) {
                ids.add(peer.userId);
            }
        }

        return [...ids].sort();
    }, [currentUserId, filtered, participantsByConversation, tab]);
    const directPeerUserIdsKey = directPeerUserIds.join("|");

    // Прогреваем participants для всех direct-чатов в списке. participants-store
    // сам дедуплицирует параллельные запросы по conversationId и кэширует на 5 мин.
    useEffect(() => {
        if (tab !== "chats") return;
        const load = useParticipantsStore.getState().load;
        for (const c of filtered) {
            if (
                c.type === "Direct" &&
                !c.lastMessagePreview &&
                !(messagesByConversation[c.id]?.length ?? 0)
            ) {
                continue;
            }

            if (!participantsByConversation[c.id]) {
                load(c.id).catch(() => {
                    /* fail-open: останется "Личный чат" до retry */
                });
            }
        }
    }, [filtered, messagesByConversation, tab, participantsByConversation]);

    useEffect(() => {
        if (tab !== "chats" || directPeerUserIds.length === 0) return;

        for (const userId of directPeerUserIds) {
            void watchUserPresence(userId);
        }

        return () => {
            for (const userId of directPeerUserIds) {
                unwatchUserPresence(userId);
            }
        };
    }, [directPeerUserIdsKey, tab, unwatchUserPresence, watchUserPresence]);

    return useMemo(
        () =>
            filtered.map((c) => {
                const latestMessage = messagesByConversation[c.id]?.[0];
                const typers = (
                    typingByConversation[toPresenceTypingConversationId(c.id)] ?? []
                ).filter((typer) => typer.userId !== currentUserId);
                const typingPreview = formatTypingPreview(
                    typers,
                    c.type !== "Direct",
                );
                const localUnreadCount =
                    unreadByConversation[c.id] ??
                    c.unreadCount ??
                    (currentUserId &&
                    latestMessage &&
                    latestMessage.senderId !== currentUserId &&
                    latestMessage.readAt === null
                        ? 1
                        : 0);

                if (c.type !== "Direct") {
                    return mapConversation(
                        c,
                        currentUserId,
                        null,
                        null,
                        latestMessage,
                        localUnreadCount,
                        typingPreview,
                    );
                }
                const items = participantsByConversation[c.id]?.items;
                const peer = items?.find((p) => p.userId !== currentUserId)
                    ?? items?.[0];
                // Бэк fail-open отдаёт пустую строку при недоступном UserService;
                // нормализуем в null, чтобы сработал fallback "Личный чат" в mapConversation.
                const peerName = peer?.displayName?.trim() ? peer.displayName : null;
                const peerAvatar = peer?.avatarUrl?.trim() ? peer.avatarUrl : null;
                return mapConversation(
                    c,
                    currentUserId,
                    peerName,
                    peerAvatar,
                    latestMessage,
                    localUnreadCount,
                    typingPreview,
                );
            }),
        [
            filtered,
            currentUserId,
            participantsByConversation,
            messagesByConversation,
            unreadByConversation,
            typingByConversation,
        ],
    );
};
