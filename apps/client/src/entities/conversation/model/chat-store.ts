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
import { useAuthStore } from "@/shared/store/auth-store";

/** Локальный (только клиентский) статус сообщения, не приходящий с сервера. */
export type LocalMessageStatus = "sending" | "sent" | "failed";

/** MessageDto + локальная разметка статуса для optimistic UI. */
export type LocalMessageDto = MessageDto & {
    _localStatus?: LocalMessageStatus;
};

export type ChatMessagePropsMapped = {
    id: string;
    text: string;
    isOwn: boolean;
    time: string;
    avatarUrl: string;
    showAvatar?: boolean;
    seen?: boolean;
    attachments?: { name: string; url: string }[];
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
    messagesByConversation: Record<string, MessageDto[]>;
    cursors: Record<string, string | undefined>;
    hasMoreByConversation: Record<string, boolean>;
    isLoading: boolean;
    pendingScrollToMessageId: string | null;
    threadEventListeners: Set<ThreadEventHandler>;

    connect: () => Promise<void>;
    disconnect: () => Promise<void>;

    loadConversations: (type?: "Direct" | "Discipline") => Promise<void>;
    loadMessages: (chatId: string, type: "chat" | "subject", reset?: boolean) => Promise<void>;
    loadMore: (chatId: string, type: "chat" | "subject") => Promise<void>;
    sendMessage: (
        chatId: string,
        text: string,
        attachments?: string[],
        replyToMessageId?: string,
    ) => Promise<void>;
    markRead: (chatId: string, messageId: string) => Promise<void>;

    editMessage: (messageId: string, body: string) => Promise<void>;
    deleteMessage: (messageId: string, mode: DeleteMode) => Promise<void>;
    forwardMessages: (targetConversationId: string, messageIds: string[]) => Promise<void>;
    addReaction: (messageId: string, emoji: string) => Promise<void>;
    removeReaction: (messageId: string, emoji: string) => Promise<void>;
    pinMessage: (conversationId: string, messageId: string) => Promise<void>;
    unpinMessage: (conversationId: string, messageId: string) => Promise<void>;

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

export const useChatStore = create<ChatState>((set, get) => ({
    connection: null,
    isConnected: false,
    conversations: [],
    messagesByConversation: {},
    cursors: {},
    hasMoreByConversation: {},
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
            get().addMessage(message);
        });

        newConnection.on("ConversationUpdated", (conversation: ConversationPreview) => {
            get().updateConversation(conversation);
        });

        newConnection.on(
            "MessageReadUpdate",
            (conversationId: string, upToMessageId: string, _readerUserId: string) => {
                set((state) => {
                    const msgs = state.messagesByConversation[conversationId];
                    if (!msgs) return state;

                    const readIndex = msgs.findIndex((m) => m.id === upToMessageId);
                    if (readIndex === -1) return state;

                    const updated = msgs.map((m, idx) => {
                        if (idx >= readIndex && m.readAt === null) {
                            return { ...m, readAt: new Date().toISOString() };
                        }
                        return m;
                    });

                    return {
                        messagesByConversation: {
                            ...state.messagesByConversation,
                            [conversationId]: updated,
                        },
                    };
                });
            },
        );

        newConnection.on("MessageEdited", (message: MessageDto) => {
            get().applyMessageEdited(message);
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
                get().applyPinsUpdated(conversationId, pinned);
            },
        );

        newConnection.on(
            "ThreadReplyReceived",
            (rootMessageId: string, reply: MessageDto) => {
                get().threadEventListeners.forEach((listener) =>
                    listener({ kind: "ThreadReplyReceived", rootMessageId, reply }),
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
        try {
            const res = await apiClient.chat.getConversations(type, undefined, 50);
            set({ conversations: res.items });
        } catch (error) {
            console.error("Failed to load conversations", error);
        }
    },

    loadMessages: async (chatId, _type, reset = false) => {
        const state = get();
        const currentHasMore = state.hasMoreByConversation[chatId] ?? true;

        if (!reset && !currentHasMore) return;

        set({ isLoading: true });
        try {
            const cursor = reset ? undefined : state.cursors[chatId];
            const res = await apiClient.chat.getConversationMessages(chatId, cursor, 20, "older");

            set((prev) => ({
                messagesByConversation: {
                    ...prev.messagesByConversation,
                    [chatId]: reset
                        ? res.items
                        : [...(prev.messagesByConversation[chatId] || []), ...res.items],
                },
                cursors: {
                    ...prev.cursors,
                    [chatId]: res.nextCursor,
                },
                hasMoreByConversation: {
                    ...prev.hasMoreByConversation,
                    [chatId]: !!res.nextCursor,
                },
                isLoading: false,
            }));
        } catch (error) {
            console.error("Failed to load messages", error);
            set({ isLoading: false });
        }
    },

    loadMore: async (chatId, type) => {
        const { isLoading, hasMoreByConversation, loadMessages } = get();
        if (isLoading || !hasMoreByConversation[chatId]) return;
        await loadMessages(chatId, type, false);
    },

    sendMessage: async (chatId, text, attachments = [], replyToMessageId) => {
        const { connection } = get();
        if (connection?.state !== HubConnectionState.Connected) {
            throw new Error("ChatHub is not connected");
        }
        const currentUserId = useAuthStore.getState().userId;
        if (!currentUserId) {
            throw new Error("Not authenticated");
        }

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
            mentions: [],
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
            });
            // Sender не получает MessageReceived (recipients = participants кроме отправителя),
            // поэтому подменяем оптимистичный экземпляр на серверный сами.
            get().addMessage({ ...real, _localStatus: "sent" } as LocalMessageDto);
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
        }
    },

    editMessage: async (messageId, body) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("EditMessage", { messageId, newBody: body });
        } else {
            await apiClient.chat.editMessage(messageId, body);
        }
    },

    deleteMessage: async (messageId, mode) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("DeleteMessage", messageId, mode);
        } else {
            await apiClient.chat.deleteMessage(messageId, mode);
        }
    },

    forwardMessages: async (targetConversationId, messageIds) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("ForwardMessages", targetConversationId, messageIds);
        } else {
            await apiClient.chat.forwardMessages(targetConversationId, messageIds);
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
            await connection.invoke("PinMessage", conversationId, messageId);
        } else {
            await apiClient.chat.pinMessage(conversationId, messageId);
        }
    },

    unpinMessage: async (conversationId, messageId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("UnpinMessage", conversationId, messageId);
        } else {
            await apiClient.chat.unpinMessage(conversationId, messageId);
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
            const conversationId = message.conversationId;
            const existing = state.messagesByConversation[conversationId] || [];

            const incomingCmid = (message as LocalMessageDto).clientMessageId;
            // Если приходит реальный (не optimistic) экземпляр с тем же clientMessageId —
            // заменяем оптимистичный плейсхолдер на него.
            if (incomingCmid && !message.id.startsWith("optimistic:")) {
                const optimisticIdx = existing.findIndex(
                    (m) =>
                        (m as LocalMessageDto).clientMessageId === incomingCmid &&
                        m.id.startsWith("optimistic:"),
                );
                if (optimisticIdx !== -1) {
                    const next = [...existing];
                    next[optimisticIdx] = message;
                    return {
                        messagesByConversation: {
                            ...state.messagesByConversation,
                            [conversationId]: next,
                        },
                    };
                }
            }

            // Дедуп по реальному id (повторный MessageReceived).
            if (existing.some((m) => m.id === message.id)) {
                return state;
            }

            return {
                messagesByConversation: {
                    ...state.messagesByConversation,
                    [conversationId]: [message, ...existing],
                },
            };
        });
    },

    updateConversation: (conversation) => {
        set((state) => {
            const index = state.conversations.findIndex((c) => c.id === conversation.id);
            if (index === -1) {
                return { conversations: [conversation, ...state.conversations] };
            }

            const newConversations = [...state.conversations];
            newConversations[index] = conversation;

            newConversations.sort((a, b) => {
                const dateA = a.lastMessageAt ? new Date(a.lastMessageAt).getTime() : 0;
                const dateB = b.lastMessageAt ? new Date(b.lastMessageAt).getTime() : 0;
                return dateB - dateA;
            });

            return { conversations: newConversations };
        });
    },

    applyMessageEdited: (message) => {
        set((state) =>
            updateMessageInState(state, message.conversationId, message.id, () => message),
        );
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
            if (idx === -1) return state;
            const updated = {
                ...state.conversations[idx],
                pinnedMessageIds: pinned.map((m) => m.id),
            };
            const next = [...state.conversations];
            next[idx] = updated;

            // Если pinned-сообщения уже в буфере — обновляем их инкрементально,
            // НЕ перересортировывая весь список (иначе сообщение "телепортируется"
            // при unpin, потому что reactions/state поменялся и порядок поедет).
            const list = state.messagesByConversation[conversationId];
            let nextMessages = state.messagesByConversation;
            if (list && pinned.length > 0) {
                const pinnedById = new Map(pinned.map((p) => [p.id, p]));
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

            return { conversations: next, messagesByConversation: nextMessages };
        });
    },
}));

export const mapMessageToProps = (
    dto: MessageDto,
    currentUserId?: string | null,
): ChatMessagePropsMapped => {
    const timeStr = new Date(dto.createdAt).toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
    });
    return {
        id: dto.id,
        text: dto.body,
        isOwn: dto.senderId === currentUserId,
        time: timeStr,
        avatarUrl: "",
        seen: dto.readAt !== null,
        attachments: dto.attachments.map((a) => ({
            name: a.fileName,
            url: `/api/media/${a.mediaAssetId}/download-url`,
        })),
    };
};
