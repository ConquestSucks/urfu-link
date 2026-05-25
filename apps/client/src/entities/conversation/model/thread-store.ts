import { create } from "zustand";
import type { ActiveThreadDto, MessageDto } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";
import { playMessageSound } from "@/shared/lib/message-sounds";
import { useChatStore } from "./chat-store";
import { HubConnectionState } from "@microsoft/signalr";

type ThreadState = {
    rootsById: Record<string, MessageDto | undefined>;
    messagesByThread: Record<string, MessageDto[]>;
    cursorsByThread: Record<string, string | undefined>;
    hasMoreByThread: Record<string, boolean>;
    loadingByThread: Record<string, boolean>;
    activeThreads: ActiveThreadDto[];
    subscribed: Set<string>;

    loadThread: (rootMessageId: string, reset?: boolean) => Promise<void>;
    loadMoreThread: (rootMessageId: string) => Promise<void>;
    subscribeThread: (rootMessageId: string) => Promise<void>;
    unsubscribeThread: (rootMessageId: string) => Promise<void>;
    replyInThread: (
        rootMessageId: string,
        body: string,
        attachmentAssetIds?: string[],
        replyToMessageId?: string,
    ) => Promise<void>;
    loadActiveThreads: () => Promise<void>;
};

let chatBridgeAttached = false;

const attachChatBridge = () => {
    if (chatBridgeAttached) return;
    chatBridgeAttached = true;
    useChatStore.getState().subscribeThreadEvents((event) => {
        if (event.kind !== "ThreadReplyReceived") return;
        useThreadStore.setState((state) => {
            const list = state.messagesByThread[event.rootMessageId] ?? [];
            if (list.some((m) => m.id === event.reply.id)) return state;
            return {
                messagesByThread: {
                    ...state.messagesByThread,
                    [event.rootMessageId]: [event.reply, ...list],
                },
            };
        });
    });
};

export const useThreadStore = create<ThreadState>((set, get) => {
    attachChatBridge();
    return {
        rootsById: {},
        messagesByThread: {},
        cursorsByThread: {},
        hasMoreByThread: {},
        loadingByThread: {},
        activeThreads: [],
        subscribed: new Set<string>(),

        loadThread: async (rootMessageId, reset = false) => {
            const state = get();
            if (state.loadingByThread[rootMessageId]) return;
            const currentHasMore = state.hasMoreByThread[rootMessageId] ?? true;
            if (!reset && !currentHasMore) return;

            set((s) => ({
                loadingByThread: { ...s.loadingByThread, [rootMessageId]: true },
            }));

            try {
                const cursor = reset ? undefined : state.cursorsByThread[rootMessageId];
                const res = await apiClient.chat.getThreadMessages(rootMessageId, cursor, 30, "older");

                const root = useChatStore
                    .getState()
                    .messagesByConversation;
                let rootDto: MessageDto | undefined;
                for (const list of Object.values(root)) {
                    const found = list.find((m) => m.id === rootMessageId);
                    if (found) {
                        rootDto = found;
                        break;
                    }
                }

                set((s) => ({
                    rootsById: { ...s.rootsById, [rootMessageId]: rootDto ?? s.rootsById[rootMessageId] },
                    messagesByThread: {
                        ...s.messagesByThread,
                        [rootMessageId]: reset
                            ? res.items
                            : [...(s.messagesByThread[rootMessageId] ?? []), ...res.items],
                    },
                    cursorsByThread: {
                        ...s.cursorsByThread,
                        [rootMessageId]: res.nextCursor,
                    },
                    hasMoreByThread: {
                        ...s.hasMoreByThread,
                        [rootMessageId]: !!res.nextCursor,
                    },
                    loadingByThread: { ...s.loadingByThread, [rootMessageId]: false },
                }));
            } catch (e) {
                console.error("Failed to load thread", e);
                set((s) => ({
                    loadingByThread: { ...s.loadingByThread, [rootMessageId]: false },
                }));
            }
        },

        loadMoreThread: async (rootMessageId) => {
            const { hasMoreByThread, loadThread } = get();
            if (!hasMoreByThread[rootMessageId]) return;
            await loadThread(rootMessageId, false);
        },

        subscribeThread: async (rootMessageId) => {
            const { connection } = useChatStore.getState();
            if (connection?.state === HubConnectionState.Connected) {
                try {
                    await connection.invoke("JoinThread", rootMessageId);
                } catch (e) {
                    console.error("JoinThread failed", e);
                }
            } else {
                try {
                    await apiClient.chat.subscribeToThread(rootMessageId);
                } catch (e) {
                    console.error("subscribeToThread failed", e);
                }
            }
            set((s) => {
                const next = new Set(s.subscribed);
                next.add(rootMessageId);
                return { subscribed: next };
            });
        },

        unsubscribeThread: async (rootMessageId) => {
            const { connection } = useChatStore.getState();
            if (connection?.state === HubConnectionState.Connected) {
                try {
                    await connection.invoke("LeaveThread", rootMessageId);
                } catch (e) {
                    console.error("LeaveThread failed", e);
                }
            } else {
                try {
                    await apiClient.chat.unsubscribeFromThread(rootMessageId);
                } catch (e) {
                    console.error("unsubscribeFromThread failed", e);
                }
            }
            set((s) => {
                const next = new Set(s.subscribed);
                next.delete(rootMessageId);
                return { subscribed: next };
            });
        },

        replyInThread: async (rootMessageId, body, attachmentAssetIds = [], replyToMessageId) => {
            const { connection } = useChatStore.getState();
            if (connection?.state === HubConnectionState.Connected) {
                await connection.invoke("ReplyInThread", {
                    rootMessageId,
                    body,
                    attachmentAssetIds,
                    replyToMessageId: replyToMessageId ?? null,
                    clientMessageId: crypto.randomUUID(),
                });
            } else {
                await apiClient.chat.replyInThread(
                    rootMessageId,
                    body,
                    attachmentAssetIds,
                    replyToMessageId,
                );
            }
            void playMessageSound("send");
        },

        loadActiveThreads: async () => {
            try {
                const res = await apiClient.chat.getActiveThreads(undefined, 50);
                set({ activeThreads: res.items });
            } catch (e) {
                console.error("Failed to load active threads", e);
            }
        },
    };
});

// Стабильная пустая ссылка для тредов без сообщений — иначе inline `?? []`
// в selector-е возвращает новый массив каждый рендер, и Zustand v5 ловит
// «getSnapshot should be cached» с последующим infinite loop.
const EMPTY_THREAD_MESSAGES: MessageDto[] = [];

export const useThreadMessages = (rootMessageId: string): MessageDto[] =>
    useThreadStore(
        (state) => state.messagesByThread[rootMessageId] ?? EMPTY_THREAD_MESSAGES,
    );
