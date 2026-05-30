import { create } from "zustand";
import {
    ConversationPreview,
    DeleteMode,
    MessageDto,
    ReactionsSummary,
} from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { apiClient } from "@/shared/lib/api";
import { resolveCurrentUserId } from "@/shared/lib/current-user";
import { playMessageSound } from "@/shared/lib/message-sounds";
import { showMessageBrowserNotification } from "@/shared/lib/browser-notifications";
import { useAuthStore } from "@/shared/store/auth-store";
import { lookupParticipantName } from "./participants-store";
import {
    isDirectDraftConversation,
    normalizeConversationDraftStatus,
    withDirectDraftStatus,
    withoutDirectDraftStatus,
} from "./direct-draft-status";

/** Локальный (только клиентский) статус сообщения, не приходящий с сервера. */
export type LocalMessageStatus = "sending" | "sent" | "failed";

/** MessageDto + локальная разметка статуса для optimistic UI. */
export type LocalMessageDto = MessageDto & {
    _localStatus?: LocalMessageStatus;
};

export type ChatMessagePropsMapped = {
    id: string;
    text: string;
    kind?: "User" | "SystemCall";
    systemCall?: {
        callId: string;
        callType: "Audio" | "Video";
        status: string;
        callerId: string;
        duration?: string | null;
        endReason?: string | null;
    } | null;
    isOwn: boolean;
    time: string;
    avatarUrl: string;
    showAvatar?: boolean;
    seen?: boolean;
    attachments?: { name: string; url: string; mediaAssetId?: string }[];
};

type ThreadEvent =
    | { kind: "ThreadReplyReceived"; rootMessageId: string; reply: MessageDto }
    | {
          kind: "ThreadRootUpdated";
          conversationId: string;
          rootMessageId: string;
          replyCount: number;
          participants: string[];
          lastActivityAtUtc: string;
      };

type ThreadEventHandler = (event: ThreadEvent) => void;

type ChatState = {
    connection: HubConnection | null;
    isConnected: boolean;
    conversations: ConversationPreview[];
    isConversationsLoading: boolean;
    messagesByConversation: Record<string, MessageDto[]>;
    unreadByConversation: Record<string, number>;
    cursors: Record<string, string | undefined>;
    hasMoreByConversation: Record<string, boolean>;
    messagesLoadingByConversation: Record<string, boolean>;
    messagesLoadedByConversation: Record<string, boolean>;
    pinnedMessagesByConversation: Record<string, MessageDto[]>;
    pinnedLoadingByConversation: Record<string, boolean>;
    isLoading: boolean;
    pendingScrollToMessageId: string | null;
    threadEventListeners: Set<ThreadEventHandler>;

    connect: () => Promise<void>;
    disconnect: () => Promise<void>;

    // Фильтр для GET /conversations — бэк принимает "direct" или "discipline"
    // (case-insensitive). "Direct" → личные, "Discipline" → дисциплинные группы.
    loadConversations: (type?: "Direct" | "Discipline") => Promise<void>;
    loadMessages: (chatId: string, type: "chat" | "subject", reset?: boolean) => Promise<void>;
    loadMore: (chatId: string, type: "chat" | "subject") => Promise<void>;
    loadPinnedMessages: (conversationId: string) => Promise<void>;
    sendMessage: (
        chatId: string,
        text: string,
        attachments?: string[],
        replyToMessageId?: string,
        mentionUserIds?: string[],
    ) => Promise<void>;
    markRead: (chatId: string, messageId: string) => Promise<void>;

    editMessage: (messageId: string, body: string) => Promise<void>;
    deleteMessage: (messageId: string, mode: DeleteMode) => Promise<void>;
    forwardMessages: (targetConversationId: string, messageIds: string[]) => Promise<void>;
    addReaction: (messageId: string, emoji: string) => Promise<void>;
    removeReaction: (messageId: string, emoji: string) => Promise<void>;
    pinMessage: (conversationId: string, messageId: string) => Promise<void>;
    unpinMessage: (conversationId: string, messageId: string) => Promise<void>;
    startTyping: (conversationId: string) => void;
    stopTyping: (conversationId: string) => void;

    setPendingScrollToMessageId: (messageId: string | null) => void;
    subscribeThreadEvents: (handler: ThreadEventHandler) => () => void;

    addMessage: (message: MessageDto) => void;
    updateConversation: (conversation: ConversationPreview) => void;

    applyMessageEdited: (message: MessageDto) => void;
    applyMessageDeleted: (
        conversationId: string,
        messageId: string,
        mode: DeleteMode,
    ) => void;
    applyReactionsUpdated: (messageId: string, reactions: ReactionsSummary) => void;
    applyPinsUpdated: (conversationId: string, pinned: MessageDto[]) => void;
};

type ServerMessageDto = MessageDto & {
    createdAtUtc?: string;
    deliveredAtUtc?: string | null;
    readAtUtc?: string | null;
    reactionsSummary?: ReactionsSummary;
    deleteMode?: DeleteMode | null;
};

const normalizeMessage = (message: MessageDto): MessageDto => {
    const raw = message as ServerMessageDto;
    return {
        ...message,
        attachments: message.attachments ?? [],
        createdAt: message.createdAt || raw.createdAtUtc || "",
        deliveredAt: message.deliveredAt ?? raw.deliveredAtUtc ?? null,
        readAt: message.readAt ?? raw.readAtUtc ?? null,
        reactions: message.reactions ?? raw.reactionsSummary ?? {},
        mentions: message.mentions ?? [],
        forwardedFrom: message.forwardedFrom ?? null,
        deletedMode: message.deletedMode ?? raw.deleteMode ?? null,
    };
};

const updateMessageInState = (
    state: { messagesByConversation: Record<string, MessageDto[]> },
    conversationId: string,
    messageId: string,
    update: (msg: MessageDto) => MessageDto,
) => {
    const list = state.messagesByConversation[conversationId];
    if (!list) return state;
    let touched = false;
    const next = list.map((m) => {
        if (m.id !== messageId) return m;
        touched = true;
        return update(m);
    });
    if (!touched) return state;
    return {
        messagesByConversation: {
            ...state.messagesByConversation,
            [conversationId]: next,
        },
    };
};

const hasAnyLoadingMessages = (loading: Record<string, boolean>) =>
    Object.values(loading).some(Boolean);

const compareConversationActivity = (a: ConversationPreview, b: ConversationPreview) => {
    const dateA = a.lastMessageAtUtc ?? a.lastMessageAt;
    const dateB = b.lastMessageAtUtc ?? b.lastMessageAt;
    const timeA = dateA ? new Date(dateA).getTime() : 0;
    const timeB = dateB ? new Date(dateB).getTime() : 0;
    return timeB - timeA;
};

const updateConversationPreviewFromMessage = (
    conversations: ConversationPreview[],
    message: MessageDto,
) => {
    const index = conversations.findIndex((c) => c.id === message.conversationId);
    if (index === -1) return conversations;

    const next = [...conversations];
    const previous = next[index];
    const sentAtUtc = message.createdAt || undefined;
    const updated = {
        ...previous,
        lastMessageAtUtc: sentAtUtc ?? previous.lastMessageAtUtc,
        lastMessagePreview: {
            messageId: message.id,
            senderId: message.senderId,
            body: message.body,
            sentAtUtc,
            hasAttachments: message.attachments.length > 0,
            attachmentFileNames: message.attachments.map((a) => a.fileName),
            readAtUtc: message.readAt,
        },
    };
    const isOptimistic = message.id.startsWith("optimistic:");
    next[index] = isOptimistic
        ? isDirectDraftConversation(previous)
            ? withDirectDraftStatus(updated)
            : updated
        : withoutDirectDraftStatus(updated);

    next.sort(compareConversationActivity);
    return next;
};

const mergeConversationUpdate = (
    current: ConversationPreview,
    incoming: ConversationPreview,
): ConversationPreview => {
    const currentPreview = current.lastMessagePreview;
    const incomingPreview = incoming.lastMessagePreview;
    const unreadCount = typeof incoming.unreadCount === "number"
        ? incoming.unreadCount
        : current.unreadCount;
    if (!currentPreview || !incomingPreview) {
        return { ...incoming, unreadCount };
    }

    const currentSentAt = currentPreview.sentAtUtc ?? currentPreview.sentAt;
    const incomingSentAt = incomingPreview.sentAtUtc ?? incomingPreview.sentAt;
    const samePreview =
        (currentPreview.messageId &&
            incomingPreview.messageId &&
            currentPreview.messageId === incomingPreview.messageId) ||
        (!incomingPreview.messageId &&
            currentPreview.senderId === incomingPreview.senderId &&
            currentPreview.body === incomingPreview.body &&
            currentSentAt === incomingSentAt);

    if (!samePreview) {
        return { ...incoming, unreadCount };
    }

    return {
        ...incoming,
        unreadCount,
        lastMessagePreview: {
            ...incomingPreview,
            messageId: incomingPreview.messageId ?? currentPreview.messageId,
            readAtUtc:
                incomingPreview.readAtUtc ??
                incomingPreview.readAt ??
                currentPreview.readAtUtc ??
                currentPreview.readAt ??
                null,
            attachmentFileNames:
                incomingPreview.attachmentFileNames ??
                currentPreview.attachmentFileNames,
        },
    };
};

const replaceMessageInList = (
    list: MessageDto[] | undefined,
    messageId: string,
    replacement: MessageDto,
) => {
    if (!list) return { list, touched: false };

    let touched = false;
    const next = list.map((message) => {
        if (message.id !== messageId) return message;
        touched = true;
        return replacement;
    });

    return { list: touched ? next : list, touched };
};

const markLastPreviewRead = (
    conversations: ConversationPreview[],
    conversationId: string,
    messageId: string,
    currentUserId: string,
    readAtUtc: string,
) => {
    const index = conversations.findIndex((c) => c.id === conversationId);
    if (index === -1) return conversations;

    const conversation = conversations[index];
    const preview = conversation.lastMessagePreview;
    if (
        !preview ||
        preview.senderId !== currentUserId ||
        preview.messageId !== messageId ||
        (preview.readAtUtc ?? preview.readAt ?? null) !== null
    ) {
        return conversations;
    }

    const next = [...conversations];
    next[index] = {
        ...conversation,
        lastMessagePreview: {
            ...preview,
            readAtUtc,
        },
    };
    return next;
};

const updateConversationPreviewForEditedMessage = (
    conversations: ConversationPreview[],
    message: MessageDto,
) => {
    const index = conversations.findIndex((c) => c.id === message.conversationId);
    if (index === -1) return conversations;

    const conversation = conversations[index];
    const preview = conversation.lastMessagePreview;
    if (!preview || preview.messageId !== message.id) return conversations;

    const next = [...conversations];
    next[index] = {
        ...conversation,
        lastMessagePreview: {
            ...preview,
            body: message.body,
            hasAttachments: message.attachments.length > 0,
            attachmentFileNames: message.attachments.map((a) => a.fileName),
            readAtUtc: message.readAt,
        },
    };

    return next;
};

const withoutUnread = (unread: Record<string, number>, conversationId: string) => {
    if (!(conversationId in unread)) return unread;
    const next = { ...unread };
    delete next[conversationId];
    return next;
};

const incrementUnread = (
    unread: Record<string, number>,
    conversationId: string,
    baseline = 0,
) => ({
    ...unread,
    [conversationId]: (unread[conversationId] ?? baseline) + 1,
});

const updateConversationUnreadCount = (
    conversations: ConversationPreview[],
    conversationId: string,
    unreadCount: number,
) => {
    const index = conversations.findIndex((c) => c.id === conversationId);
    if (index === -1 || conversations[index].unreadCount === unreadCount) {
        return conversations;
    }

    const next = [...conversations];
    next[index] = {
        ...next[index],
        unreadCount,
    };
    return next;
};

const markIncomingReadInState = (
    state: Pick<ChatState, "conversations" | "messagesByConversation" | "unreadByConversation">,
    conversationId: string,
    upToMessageId: string,
    currentUserId: string | null,
) => {
    const list = state.messagesByConversation[conversationId];
    const unreadByConversation = withoutUnread(state.unreadByConversation, conversationId);
    const conversations = updateConversationUnreadCount(state.conversations, conversationId, 0);
    if (!list || !currentUserId) {
        return { conversations, unreadByConversation };
    }

    const readIndex = list.findIndex((m) => m.id === upToMessageId);
    if (readIndex === -1) {
        return { conversations, unreadByConversation };
    }

    const readAt = new Date().toISOString();
    return {
        messagesByConversation: {
            ...state.messagesByConversation,
            [conversationId]: list.map((m, idx) =>
                idx >= readIndex && m.senderId !== currentUserId && m.readAt === null
                    ? { ...m, readAt }
                    : m,
            ),
        },
        conversations,
        unreadByConversation,
    };
};

export const useChatStore = create<ChatState>((set, get) => ({
    connection: null,
    isConnected: false,
    conversations: [],
    isConversationsLoading: false,
    messagesByConversation: {},
    unreadByConversation: {},
    cursors: {},
    hasMoreByConversation: {},
    messagesLoadingByConversation: {},
    messagesLoadedByConversation: {},
    pinnedMessagesByConversation: {},
    pinnedLoadingByConversation: {},
    isLoading: false,
    pendingScrollToMessageId: null,
    threadEventListeners: new Set<ThreadEventHandler>(),

    connect: async () => {
        const { connection, isConnected } = get();
        if (isConnected || connection?.state === HubConnectionState.Connected) {
            return;
        }

        const newConnection = createHubConnection("/hubs/chat");

        newConnection.on("MessageReceived", (message: MessageDto) => {
            const normalized = normalizeMessage(message);
            const state = get();
            const existing = state.messagesByConversation[normalized.conversationId] ?? [];
            const alreadyKnown = existing.some((m) => m.id === normalized.id);
            const currentUserId = useAuthStore.getState().userId;

            get().addMessage(normalized);

            if (!alreadyKnown && currentUserId && normalized.senderId !== currentUserId) {
                const conversation = state.conversations.find(
                    (c) => c.id === normalized.conversationId,
                );
                const isDiscipline =
                    conversation?.groupSubtype === "Discipline" ||
                    normalized.conversationId.startsWith("discipline:");
                void playMessageSound("receive", {
                    conversationId: normalized.conversationId,
                    isDiscipline,
                });
                showMessageBrowserNotification(normalized, {
                    currentUserId,
                    title:
                        lookupParticipantName(normalized.conversationId, normalized.senderId) ??
                        conversation?.title ??
                        "Новое сообщение",
                    isDiscipline,
                });
            }
        });

        newConnection.on("ConversationUpdated", (conversation: ConversationPreview) => {
            get().updateConversation(conversation);
        });

        newConnection.on(
            "MessageReadUpdate",
            (conversationId: string, upToMessageId: string, readerUserId: string) => {
                const currentUserId = useAuthStore.getState().userId;
                if (!currentUserId || readerUserId === currentUserId) {
                    return;
                }

                set((state) => {
                    const msgs = state.messagesByConversation[conversationId];
                    if (!msgs) return state;

                    const readIndex = msgs.findIndex((m) => m.id === upToMessageId);
                    if (readIndex === -1) return state;

                    const updated = msgs.map((m, idx) => {
                        if (idx >= readIndex && m.senderId === currentUserId && m.readAt === null) {
                            return { ...m, readAt: new Date().toISOString() };
                        }
                        return m;
                    });

                    return {
                        messagesByConversation: {
                            ...state.messagesByConversation,
                            [conversationId]: updated,
                        },
                        conversations: markLastPreviewRead(
                            state.conversations,
                            conversationId,
                            upToMessageId,
                            currentUserId,
                            new Date().toISOString(),
                        ),
                    };
                });
            },
        );

        newConnection.on(
            "MessageReadByUpdate",
            (
                conversationId: string,
                messageId: string,
                readerUserId: string,
                readAtUtc: string,
            ) => {
                const currentUserId = useAuthStore.getState().userId;
                if (!currentUserId || readerUserId === currentUserId) {
                    return;
                }

                set((state) => {
                    const list = state.messagesByConversation[conversationId];
                    let messagesByConversation = state.messagesByConversation;
                    if (list) {
                        let touched = false;
                        const next = list.map((m) => {
                            if (
                                m.id === messageId &&
                                m.senderId === currentUserId &&
                                m.readAt === null
                            ) {
                                touched = true;
                                return { ...m, readAt: readAtUtc };
                            }
                            return m;
                        });
                        if (touched) {
                            messagesByConversation = {
                                ...state.messagesByConversation,
                                [conversationId]: next,
                            };
                        }
                    }

                    const conversations = markLastPreviewRead(
                        state.conversations,
                        conversationId,
                        messageId,
                        currentUserId,
                        readAtUtc,
                    );
                    if (
                        messagesByConversation === state.messagesByConversation &&
                        conversations === state.conversations
                    ) {
                        return state;
                    }

                    return { messagesByConversation, conversations };
                });
            },
        );

        newConnection.on("MessageEdited", (message: MessageDto) => {
            get().applyMessageEdited(normalizeMessage(message));
        });

        newConnection.on(
            "MessageDeletedUpdate",
            (
                conversationId: string,
                messageId: string,
                mode: DeleteMode,
                _deletedBy: string,
            ) => {
                get().applyMessageDeleted(conversationId, messageId, mode);
            },
        );

        newConnection.on(
            "ReactionUpdated",
            (messageId: string, reactions: ReactionsSummary) => {
                get().applyReactionsUpdated(messageId, reactions);
            },
        );

        newConnection.on(
            "PinsUpdated",
            (conversationId: string, pinned: MessageDto[]) => {
                get().applyPinsUpdated(conversationId, pinned.map(normalizeMessage));
            },
        );

        newConnection.on(
            "ThreadReplyReceived",
            (rootMessageId: string, reply: MessageDto) => {
                get().threadEventListeners.forEach((listener) =>
                    listener({ kind: "ThreadReplyReceived", rootMessageId, reply: normalizeMessage(reply) }),
                );
            },
        );

        newConnection.on(
            "ThreadRootUpdated",
            (
                conversationId: string,
                rootMessageId: string,
                replyCount: number,
                participants: string[],
                lastActivityAtUtc: string,
            ) => {
                set((state) =>
                    updateMessageInState(state, conversationId, rootMessageId, (m) => ({
                        ...m,
                        threadReplyCount: replyCount,
                        threadParticipants: participants,
                        threadLastReplyAtUtc: lastActivityAtUtc,
                    })),
                );

                get().threadEventListeners.forEach((listener) =>
                    listener({
                        kind: "ThreadRootUpdated",
                        conversationId,
                        rootMessageId,
                        replyCount,
                        participants,
                        lastActivityAtUtc,
                    }),
                );
            },
        );

        newConnection.onclose((err) => {
            if (err) {
                console.warn("ChatHub connection closed with error", err);
            }
            set({ isConnected: false });
        });

        newConnection.onreconnected(() => {
            // После потери соединения часть сообщений могла пройти мимо. Сбрасываем
            // курсоры и hasMore, чтобы следующий loadMessages(reset=true) подтянул
            // свежий снэпшот.
            set({ cursors: {}, hasMoreByConversation: {}, isConnected: true });
        });

        newConnection.onreconnecting((err) => {
            console.warn("ChatHub reconnecting", err);
            set({ isConnected: false });
        });

        try {
            await newConnection.start();
            set({ connection: newConnection, isConnected: true });
        } catch (e) {
            console.error("Failed to connect to ChatHub", e);
            set({ isConnected: false });
        }
    },

    disconnect: async () => {
        const { connection } = get();
        if (connection) {
            await connection.stop();
            set({ connection: null, isConnected: false });
        }
    },

    loadConversations: async (type) => {
        set({ isConversationsLoading: true });
        try {
            const res = await apiClient.chat.getConversations(type, undefined, 50);
            set((state) => {
                const unreadByConversation = { ...state.unreadByConversation };
                for (const conversation of res.items) {
                    if (typeof conversation.unreadCount !== "number") continue;
                    if (conversation.unreadCount > 0) {
                        unreadByConversation[conversation.id] = conversation.unreadCount;
                    } else {
                        delete unreadByConversation[conversation.id];
                    }
                }

                return {
                    conversations: res.items.map(normalizeConversationDraftStatus),
                    unreadByConversation,
                    isConversationsLoading: false,
                };
            });
        } catch (error) {
            console.error("Failed to load conversations", error);
            set({ isConversationsLoading: false });
        }
    },

    loadMessages: async (chatId, _type, reset = false) => {
        const state = get();
        const currentHasMore = state.hasMoreByConversation[chatId] ?? true;

        if (!reset && !currentHasMore) return;

        set((prev) => ({
            messagesLoadingByConversation: {
                ...prev.messagesLoadingByConversation,
                [chatId]: true,
            },
            isLoading: true,
        }));
        try {
            const cursor = reset ? undefined : state.cursors[chatId];
            const res = await apiClient.chat.getConversationMessages(chatId, cursor, 20, "older");
            const items = res.items.map(normalizeMessage);
            const currentUserId = useAuthStore.getState().userId;

            set((prev) => {
                const messagesLoadingByConversation = {
                    ...prev.messagesLoadingByConversation,
                    [chatId]: false,
                };

                const unreadCount = currentUserId
                    ? items.filter((m) => m.senderId !== currentUserId && m.readAt === null).length
                    : 0;
                const unreadByConversation = reset
                    ? unreadCount > 0
                        ? {
                              ...prev.unreadByConversation,
                              [chatId]: unreadCount,
                          }
                        : withoutUnread(prev.unreadByConversation, chatId)
                    : prev.unreadByConversation;

                return {
                    messagesByConversation: {
                        ...prev.messagesByConversation,
                        [chatId]: reset
                            ? items
                            : [...(prev.messagesByConversation[chatId] || []), ...items],
                    },
                    cursors: {
                        ...prev.cursors,
                        [chatId]: res.nextCursor,
                    },
                    hasMoreByConversation: {
                        ...prev.hasMoreByConversation,
                        [chatId]: !!res.nextCursor,
                    },
                    messagesLoadingByConversation,
                    messagesLoadedByConversation: {
                        ...prev.messagesLoadedByConversation,
                        [chatId]: true,
                    },
                    conversations: reset
                        ? updateConversationUnreadCount(prev.conversations, chatId, unreadCount)
                        : prev.conversations,
                    unreadByConversation,
                    isLoading: hasAnyLoadingMessages(messagesLoadingByConversation),
                };
            });
        } catch (error) {
            console.error("Failed to load messages", error);
            set((prev) => {
                const messagesLoadingByConversation = {
                    ...prev.messagesLoadingByConversation,
                    [chatId]: false,
                };

                return {
                    messagesLoadingByConversation,
                    isLoading: hasAnyLoadingMessages(messagesLoadingByConversation),
                };
            });
        }
    },

    loadMore: async (chatId, type) => {
        const { messagesLoadingByConversation, hasMoreByConversation, loadMessages } = get();
        if (messagesLoadingByConversation[chatId] || !hasMoreByConversation[chatId]) return;
        await loadMessages(chatId, type, false);
    },

    loadPinnedMessages: async (conversationId) => {
        set((state) => ({
            pinnedLoadingByConversation: {
                ...state.pinnedLoadingByConversation,
                [conversationId]: true,
            },
        }));

        try {
            const pinned = await apiClient.chat.getPinnedMessages(conversationId);
            get().applyPinsUpdated(conversationId, pinned.map(normalizeMessage));
        } catch (error) {
            console.error("Failed to load pinned messages", error);
        } finally {
            set((state) => ({
                pinnedLoadingByConversation: {
                    ...state.pinnedLoadingByConversation,
                    [conversationId]: false,
                },
            }));
        }
    },

    sendMessage: async (chatId, text, attachments = [], replyToMessageId, mentionUserIds = []) => {
        const { connection } = get();
        if (connection?.state !== HubConnectionState.Connected) {
            throw new Error("ChatHub is not connected");
        }
        const currentUserId = await resolveCurrentUserId();

        const clientMessageId =
            typeof crypto !== "undefined" && "randomUUID" in crypto
                ? crypto.randomUUID()
                : `cmid-${Date.now()}-${Math.random().toString(36).slice(2)}`;

        // Поднимаем reply preview из локального состояния — для optimistic-карточки
        // достаточно messageId/senderId/preview-текста, сервер вернёт авторитетную версию.
        const replyTo = (() => {
            if (!replyToMessageId) return null;
            const list = get().messagesByConversation[chatId] ?? [];
            const target = list.find((m) => m.id === replyToMessageId);
            if (!target) return null;
            return {
                messageId: target.id,
                senderId: target.senderId,
                preview: target.body,
            };
        })();

        const nowIso = new Date().toISOString();
        const optimistic: LocalMessageDto = {
            id: `optimistic:${clientMessageId}`,
            conversationId: chatId,
            senderId: currentUserId,
            body: text,
            attachments: [],
            state: "Sent",
            createdAt: nowIso,
            deliveredAt: null,
            readAt: null,
            clientMessageId,
            editedAtUtc: null,
            replyTo,
            reactions: {},
            mentions: mentionUserIds,
            forwardedFrom: null,
            _localStatus: "sending",
        };

        get().addMessage(optimistic);

        try {
            // ChatHub.SendMessage принимает один record SendMessageHubInput, а не
            // позиционные аргументы — JSON-биндинг иначе не совпадает с C# record'ом
            // и invoke падает на сервере.
            const real = await connection.invoke<MessageDto>("SendMessage", {
                conversationId: chatId,
                body: text,
                attachmentAssetIds: attachments,
                clientMessageId,
                replyToMessageId: replyToMessageId ?? null,
                mentionUserIds,
                peerUserId:
                    get().conversations
                        .find((c) => c.id === chatId && c.type === "Direct")
                        ?.participants.find((id) => id !== currentUserId) ?? null,
            });
            // Sender не получает MessageReceived (recipients = participants кроме отправителя),
            // поэтому подменяем оптимистичный экземпляр на серверный сами.
            get().addMessage(normalizeMessage({ ...real, _localStatus: "sent" } as LocalMessageDto));
            void playMessageSound("send");
        } catch (error) {
            console.error("Failed to send message", error);
            set((state) => {
                const list = state.messagesByConversation[chatId] ?? [];
                return {
                    messagesByConversation: {
                        ...state.messagesByConversation,
                        [chatId]: list.map((m) =>
                            (m as LocalMessageDto).clientMessageId === clientMessageId
                                ? ({ ...m, _localStatus: "failed" } as LocalMessageDto)
                                : m,
                        ),
                    },
                };
            });
            throw error;
        }
    },

    markRead: async (chatId, messageId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("MarkRead", chatId, messageId);
            const currentUserId = useAuthStore.getState().userId;
            set((state) =>
                markIncomingReadInState(state, chatId, messageId, currentUserId ?? null),
            );
        }
    },

    editMessage: async (messageId, body) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("EditMessage", { messageId, newBody: body });
        } else {
            const updated = await apiClient.chat.editMessage(messageId, body);
            get().applyMessageEdited(normalizeMessage(updated));
        }
    },

    deleteMessage: async (messageId, mode) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("DeleteMessage", messageId, mode);
        } else {
            const deleted = await apiClient.chat.deleteMessage(messageId, mode);
            if (deleted) {
                get().applyMessageEdited(normalizeMessage(deleted));
            }
        }
    },

    forwardMessages: async (targetConversationId, messageIds) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("ForwardMessages", targetConversationId, messageIds);
        } else {
            const forwarded = await apiClient.chat.forwardMessages(targetConversationId, messageIds);
            forwarded.map(normalizeMessage).forEach((message) => get().addMessage(message));
        }
    },

    addReaction: async (messageId, emoji) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("AddReaction", messageId, emoji);
        } else {
            await apiClient.chat.addReaction(messageId, emoji);
        }
    },

    removeReaction: async (messageId, emoji) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("RemoveReaction", messageId, emoji);
        } else {
            await apiClient.chat.removeReaction(messageId, emoji);
        }
    },

    pinMessage: async (conversationId, messageId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            const pinned = await connection.invoke<MessageDto[]>(
                "PinMessage",
                conversationId,
                messageId,
            );
            get().applyPinsUpdated(conversationId, pinned.map(normalizeMessage));
        } else {
            const pinned = await apiClient.chat.pinMessage(conversationId, messageId);
            get().applyPinsUpdated(conversationId, pinned.map(normalizeMessage));
        }
    },

    unpinMessage: async (conversationId, messageId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            const pinned = await connection.invoke<MessageDto[]>(
                "UnpinMessage",
                conversationId,
                messageId,
            );
            get().applyPinsUpdated(conversationId, pinned.map(normalizeMessage));
        } else {
            const pinned = await apiClient.chat.unpinMessage(conversationId, messageId);
            get().applyPinsUpdated(conversationId, pinned.map(normalizeMessage));
        }
    },

    startTyping: (conversationId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            connection.invoke("StartTyping", conversationId).catch((error) =>
                console.warn("StartTyping failed", error),
            );
        }
    },

    stopTyping: (conversationId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            connection.invoke("StopTyping", conversationId).catch((error) =>
                console.warn("StopTyping failed", error),
            );
        }
    },

    setPendingScrollToMessageId: (messageId) => set({ pendingScrollToMessageId: messageId }),

    subscribeThreadEvents: (handler) => {
        const { threadEventListeners } = get();
        threadEventListeners.add(handler);
        return () => {
            threadEventListeners.delete(handler);
        };
    },

    addMessage: (message) => {
        set((state) => {
            const normalized = normalizeMessage(message);
            const conversationId = normalized.conversationId;
            const existing = state.messagesByConversation[conversationId] || [];

            const incomingCmid = (normalized as LocalMessageDto).clientMessageId;
            // Если приходит реальный (не optimistic) экземпляр с тем же clientMessageId —
            // заменяем оптимистичный плейсхолдер на него.
            if (incomingCmid && !normalized.id.startsWith("optimistic:")) {
                const optimisticIdx = existing.findIndex(
                    (m) =>
                        (m as LocalMessageDto).clientMessageId === incomingCmid &&
                        m.id.startsWith("optimistic:"),
                );
                if (optimisticIdx !== -1) {
                    const next = [...existing];
                    next[optimisticIdx] = normalized;
                    return {
                        messagesByConversation: {
                            ...state.messagesByConversation,
                            [conversationId]: next,
                        },
                        conversations: updateConversationPreviewFromMessage(
                            state.conversations,
                            normalized,
                        ),
                    };
                }
            }

            // Дедуп по реальному id (повторный MessageReceived).
            if (existing.some((m) => m.id === normalized.id)) {
                return state;
            }

            const currentUserId = useAuthStore.getState().userId;
            const isIncomingUnread =
                !!currentUserId &&
                normalized.senderId !== currentUserId &&
                normalized.readAt === null &&
                !normalized.id.startsWith("optimistic:");
            const serverUnreadCount = state.conversations.find((c) => c.id === conversationId)
                ?.unreadCount;
            const unreadBaseline = typeof serverUnreadCount === "number"
                ? serverUnreadCount
                : 0;

            return {
                messagesByConversation: {
                    ...state.messagesByConversation,
                    [conversationId]: [normalized, ...existing],
                },
                conversations: updateConversationPreviewFromMessage(
                    state.conversations,
                    normalized,
                ),
                unreadByConversation: isIncomingUnread
                    ? incrementUnread(state.unreadByConversation, conversationId, unreadBaseline)
                    : state.unreadByConversation,
            };
        });
    },

    updateConversation: (conversation) => {
        set((state) => {
            const normalized = normalizeConversationDraftStatus(conversation);
            const index = state.conversations.findIndex((c) => c.id === normalized.id);
            if (index === -1) {
                return { conversations: [normalized, ...state.conversations] };
            }

            const newConversations = [...state.conversations];
            newConversations[index] = mergeConversationUpdate(
                newConversations[index],
                normalized,
            );

            newConversations.sort(compareConversationActivity);

            return { conversations: newConversations };
        });
    },

    applyMessageEdited: (message) => {
        const normalized = normalizeMessage(message);
        set((state) => {
            const existingMessages = state.messagesByConversation[normalized.conversationId];
            const messagesUpdate = replaceMessageInList(
                existingMessages,
                normalized.id,
                normalized,
            );
            const existingPinned =
                state.pinnedMessagesByConversation[normalized.conversationId];
            const pinnedUpdate = replaceMessageInList(
                existingPinned,
                normalized.id,
                normalized,
            );

            return {
                messagesByConversation: messagesUpdate.touched
                    ? {
                          ...state.messagesByConversation,
                          [normalized.conversationId]: messagesUpdate.list ?? [],
                      }
                    : state.messagesByConversation,
                pinnedMessagesByConversation: pinnedUpdate.touched
                    ? {
                          ...state.pinnedMessagesByConversation,
                          [normalized.conversationId]: pinnedUpdate.list ?? [],
                      }
                    : state.pinnedMessagesByConversation,
                conversations: updateConversationPreviewForEditedMessage(
                    state.conversations,
                    normalized,
                ),
            };
        });
    },

    applyMessageDeleted: (conversationId, messageId, mode) => {
        set((state) => {
            if (mode === "for-me") {
                const list = state.messagesByConversation[conversationId];
                if (!list) return state;
                return {
                    messagesByConversation: {
                        ...state.messagesByConversation,
                        [conversationId]: list.filter((m) => m.id !== messageId),
                    },
                };
            }
            return updateMessageInState(state, conversationId, messageId, (m) => ({
                ...m,
                state: "Deleted",
                body: "",
                attachments: [],
                deletedAtUtc: new Date().toISOString(),
                deletedMode: mode,
            }));
        });
    },

    applyReactionsUpdated: (messageId, reactions) => {
        set((state) => {
            for (const conversationId of Object.keys(state.messagesByConversation)) {
                const list = state.messagesByConversation[conversationId];
                if (list.some((m) => m.id === messageId)) {
                    return updateMessageInState(state, conversationId, messageId, (m) => ({
                        ...m,
                        reactions,
                    }));
                }
            }
            return state;
        });
    },

    applyPinsUpdated: (conversationId, pinned) => {
        set((state) => {
            const idx = state.conversations.findIndex((c) => c.id === conversationId);
            const pinnedMessages = pinned.map(normalizeMessage);
            const pinnedMessageIds = pinnedMessages.map((m) => m.id);
            let next = state.conversations;
            if (idx !== -1) {
                const updated = {
                    ...state.conversations[idx],
                    pinnedMessageIds,
                };
                next = [...state.conversations];
                next[idx] = updated;
            }

            // Если pinned-сообщения уже в буфере — обновляем их инкрементально,
            // НЕ перересортировывая весь список (иначе сообщение "телепортируется"
            // при unpin, потому что reactions/state поменялся и порядок поедет).
            const list = state.messagesByConversation[conversationId];
            let nextMessages = state.messagesByConversation;
            if (list && pinnedMessages.length > 0) {
                const pinnedById = new Map(pinnedMessages.map((p) => [p.id, p]));
                let touched = false;
                const merged = list.map((m) => {
                    const fresh = pinnedById.get(m.id);
                    if (!fresh) return m;
                    touched = true;
                    return fresh;
                });
                if (touched) {
                    nextMessages = {
                        ...state.messagesByConversation,
                        [conversationId]: merged,
                    };
                }
                // Pinned messages, отсутствующие в текущем буфере, мы НЕ вставляем
                // в середину массива по createdAt — это сбивало бы pagination и
                // мог бы вызвать дубль при lazy-load. PinnedBar показывает их
                // независимо, поднимая полный pinned-snapshot через отдельный API.
            }

            return {
                conversations: next,
                messagesByConversation: nextMessages,
                pinnedMessagesByConversation: {
                    ...state.pinnedMessagesByConversation,
                    [conversationId]: pinnedMessages,
                },
            };
        });
    },
}));

export const mapMessageToProps = (
    dto: MessageDto,
    currentUserId?: string | null,
): ChatMessagePropsMapped => {
    const normalized = normalizeMessage(dto);
    const date = normalized.createdAt ? new Date(normalized.createdAt) : null;
    const timeStr = date && !Number.isNaN(date.getTime())
        ? date.toLocaleTimeString([], {
              hour: "2-digit",
              minute: "2-digit",
          })
        : "";

    return {
        id: normalized.id,
        text: normalized.body,
        kind: normalized.kind,
        systemCall: normalized.systemCall,
        isOwn: normalized.senderId === currentUserId,
        time: timeStr,
        avatarUrl: "",
        seen: normalized.readAt !== null,
        attachments: normalized.attachments.map((a) => ({
            name: a.fileName,
            url: `/api/media/${a.mediaAssetId}/download-url`,
            mediaAssetId: a.mediaAssetId,
        })),
    };
};
