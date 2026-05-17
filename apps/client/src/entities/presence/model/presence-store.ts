import { create } from "zustand";
import { PresenceInfo, PresenceStatus } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";

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
        // displayName на текущий момент не передаётся (см. follow-up issue).
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
                    store.setTyping({ userId, conversationId });
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

export const useConversationTypers = (conversationId: string) =>
    usePresenceStore((state) => state.typingByConversation[conversationId] ?? []);

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
