import { create } from "zustand";
import { PresenceInfo, PresenceStatus } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { lookupParticipantName, useParticipantsStore } from "@/entities/conversation/model/participants-store";

type TypingUser = {
    userId: string;
    conversationId: string;
    displayName?: string;
};

type PresenceState = {
    connection: HubConnection | null;
    isConnected: boolean;
    presenceByUser: Record<string, PresenceInfo>;
    typingByConversation: Record<string, TypingUser[]>;

    connect: () => Promise<void>;
    disconnect: () => Promise<void>;

    setUserPresence: (info: PresenceInfo) => void;
    setTyping: (typing: TypingUser) => void;
    clearTyping: (userId: string, conversationId: string) => void;

    sendHeartbeat: () => void;
    startTyping: (conversationId: string) => void;
    stopTyping: (conversationId: string) => void;
};

// Typing timeout — clear indicator if no update for 4 seconds
const TYPING_TIMEOUT_MS = 4000;
const typingTimers: Record<string, ReturnType<typeof setTimeout>> = {};

export const usePresenceStore = create<PresenceState>((set, get) => ({
    connection: null,
    isConnected: false,
    presenceByUser: {},
    typingByConversation: {},

    connect: async () => {
        const { connection, isConnected } = get();
        if (isConnected || connection?.state === HubConnectionState.Connected) {
            return;
        }

        const newConnection = createHubConnection("/hubs/presence");

        newConnection.on("UserPresenceChanged", (info: PresenceInfo) => {
            get().setUserPresence(info);
        });

        // Сервер шлёт UserTyping(conversationId, userId, isTyping) — см.
        // IPresenceClient.cs и PresenceBroadcaster.BroadcastTypingAsync.
        // displayName сервер не передаёт намеренно: имя резолвится на клиенте
        // через participants-store (он уже загружен для @mentions и
        // sender-фильтра в этом же чате) — без доп. gRPC-хопов Chat→Presence→User.
        newConnection.on(
            "UserTyping",
            (conversationId: string, userId: string, isTyping: boolean) => {
                const store = get();
                const key = `${conversationId}:${userId}`;

                if (typingTimers[key]) {
                    clearTimeout(typingTimers[key]);
                    delete typingTimers[key];
                }

                if (isTyping) {
                    let displayName = lookupParticipantName(conversationId, userId);
                    store.setTyping({ userId, conversationId, displayName });

                    // Кэш мог быть холодным: типинг прилетел до того, как UI
                    // успел запросить participants. Прогреваем кэш — потом
                    // следующий тик селектора useConversationTypers подтянет имя.
                    if (!displayName) {
                        useParticipantsStore
                            .getState()
                            .load(conversationId)
                            .then(() => {
                                const resolved = lookupParticipantName(conversationId, userId);
                                if (resolved) {
                                    get().setTyping({ userId, conversationId, displayName: resolved });
                                }
                            })
                            .catch(() => {
                                /* fail-open: индикатор покажется без имени */
                            });
                    }

                    // Защита от потерянного StopTyping: чистим запись, если очередной
                    // StartTyping не подоспеет за TYPING_TIMEOUT_MS.
                    typingTimers[key] = setTimeout(() => {
                        store.clearTyping(userId, conversationId);
                        delete typingTimers[key];
                    }, TYPING_TIMEOUT_MS);
                } else {
                    store.clearTyping(userId, conversationId);
                }
            },
        );

        try {
            await newConnection.start();
            set({ connection: newConnection, isConnected: true });
        } catch (e) {
            console.error("Failed to connect to PresenceHub", e);
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

    setUserPresence: (info) => {
        set((state) => ({
            presenceByUser: {
                ...state.presenceByUser,
                [info.userId]: info,
            },
        }));
    },

    setTyping: (typing) => {
        set((state) => {
            const existing = state.typingByConversation[typing.conversationId] || [];
            const filtered = existing.filter((u) => u.userId !== typing.userId);
            return {
                typingByConversation: {
                    ...state.typingByConversation,
                    [typing.conversationId]: [...filtered, typing],
                },
            };
        });
    },

    clearTyping: (userId, conversationId) => {
        set((state) => {
            const existing = state.typingByConversation[conversationId] || [];
            const filtered = existing.filter((u) => u.userId !== userId);
            return {
                typingByConversation: {
                    ...state.typingByConversation,
                    [conversationId]: filtered,
                },
            };
        });
    },

    sendHeartbeat: () => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            connection.invoke("Heartbeat").catch((e) =>
                console.warn("Heartbeat failed", e)
            );
        }
    },

    startTyping: (conversationId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            connection.invoke("StartTyping", conversationId).catch((e) =>
                console.warn("StartTyping failed", e)
            );
        }
    },

    stopTyping: (conversationId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            connection.invoke("StopTyping", conversationId).catch((e) =>
                console.warn("StopTyping failed", e)
            );
        }
    },
}));

// Selectors for convenience
export const useUserPresence = (userId: string) =>
    usePresenceStore((state) => state.presenceByUser[userId]);

// Стабильная пустая ссылка для отсутствующего conversationId — иначе каждый рендер
// возвращает новый `[]`, и Zustand v5 ловит «getSnapshot should be cached» с
// последующим infinite loop в useSyncExternalStore.
const EMPTY_TYPERS: TypingUser[] = [];

export const useConversationTypers = (conversationId: string): TypingUser[] =>
    usePresenceStore(
        (state) => state.typingByConversation[conversationId] ?? EMPTY_TYPERS,
    );

export const presenceStatusToLabel = (status: PresenceStatus): string => {
    switch (status) {
        case "Online": return "В сети";
        case "Away": return "Отошёл";
        case "DoNotDisturb": return "Не беспокоить";
        case "Invisible":
        case "Offline": return "Не в сети";
        default: return "";
    }
};
