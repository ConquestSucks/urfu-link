import { create } from "zustand";
import { useMemo } from "react";
import type { Platform, PresenceInfo, PresenceStatus } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { lookupParticipantName } from "@/entities/conversation/model/participants-store";

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
    watchedUserIds: string[];
    watchedUserRefCounts: Record<string, number>;

    connect: () => Promise<void>;
    disconnect: () => Promise<void>;
    watchUserPresence: (userId: string) => Promise<void>;
    unwatchUserPresence: (userId: string) => void;

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
const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const COMPACT_HEX_RE = /^[0-9a-f]{32,}$/i;
const DISCIPLINE_PREFIX = "discipline:";
const PRESENCE_STATUSES = ["Online", "Away", "DoNotDisturb", "Invisible", "Offline"] as const;
const PLATFORMS = ["Mobile", "Web", "Desktop"] as const;

const isUuid = (value: string) => UUID_RE.test(value);

const isConnectionClosingError = (error: unknown) =>
    error instanceof Error &&
    error.message.toLowerCase().includes("underlying connection being closed");

const warnUnlessConnectionClosingError = (message: string, error: unknown) => {
    if (!isConnectionClosingError(error)) {
        console.warn(message, error);
    }
};

const formatGuid = (hex: string) =>
    `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20, 32)}`.toLowerCase();

const normalizePresenceStatus = (status: PresenceStatus | number | string): PresenceStatus => {
    if (typeof status === "number") {
        return PRESENCE_STATUSES[status] ?? "Offline";
    }

    return PRESENCE_STATUSES.includes(status as PresenceStatus)
        ? status as PresenceStatus
        : "Offline";
};

const normalizePlatform = (platform: Platform | number | string): Platform | null => {
    if (typeof platform === "number") {
        return PLATFORMS[platform] ?? null;
    }

    return PLATFORMS.includes(platform as Platform)
        ? platform as Platform
        : null;
};

const normalizePlatforms = (platforms: Array<Platform | number | string>): Platform[] =>
    platforms
        .map(normalizePlatform)
        .filter((platform): platform is Platform => platform !== null);

const isActiveConnection = (connection: HubConnection | null) =>
    connection !== null && connection.state !== HubConnectionState.Disconnected;

export const toPresenceTypingConversationId = (conversationId: string) => {
    if (GUID_RE.test(conversationId)) {
        return conversationId.toLowerCase();
    }

    if (conversationId.startsWith(DISCIPLINE_PREFIX)) {
        const disciplineId = conversationId.slice(DISCIPLINE_PREFIX.length);
        const compact = disciplineId.replace(/-/g, "");
        return COMPACT_HEX_RE.test(compact)
            ? formatGuid(compact.slice(0, 32))
            : conversationId;
    }

    const compact = conversationId.replace(/-/g, "");
    return COMPACT_HEX_RE.test(compact)
        ? formatGuid(compact.slice(0, 32))
        : conversationId;
};

export const usePresenceStore = create<PresenceState>((set, get) => ({
    connection: null,
    isConnected: false,
    presenceByUser: {},
    typingByConversation: {},
    watchedUserIds: [],
    watchedUserRefCounts: {},

    connect: async () => {
        const { connection, isConnected } = get();
        if (isConnected || isActiveConnection(connection)) {
            return;
        }

        const newConnection = createHubConnection("/hubs/presence");
        const subscribeWatchedUsers = async () => {
            const watched = get().watchedUserIds.filter(isUuid);
            if (
                watched.length === 0 ||
                newConnection.state !== HubConnectionState.Connected ||
                get().connection !== newConnection
            ) {
                return;
            }

            await newConnection.invoke("SubscribeToUsers", watched).catch((error) =>
                warnUnlessConnectionClosingError("Presence subscribe failed", error),
            );
        };

        newConnection.on(
            "UserPresenceChanged",
            (
                userId: string,
                status: PresenceStatus | number | string,
                platforms: Array<Platform | number | string>,
                lastSeenAt?: string | null,
            ) => {
                get().setUserPresence({
                    userId,
                    status: normalizePresenceStatus(status),
                    platforms: normalizePlatforms(platforms),
                    lastSeenAt: lastSeenAt ?? null,
                });
            },
        );

        // Сервер шлёт UserTyping(conversationId, userId, isTyping) — см.
        // IPresenceClient.cs и PresenceBroadcaster.BroadcastTypingAsync.
        // displayName сервер не передаёт намеренно: имя резолвится на клиенте
        // через participants-store (он уже загружен для @mentions и
        // sender-фильтра в этом же чате) — без доп. gRPC-хопов Chat→Presence→User.
        newConnection.on(
            "UserTyping",
            (conversationId: string, userId: string, isTyping: boolean) => {
                const store = get();
                const presenceConversationId = toPresenceTypingConversationId(conversationId);
                const key = `${presenceConversationId}:${userId}`;

                if (typingTimers[key]) {
                    clearTimeout(typingTimers[key]);
                    delete typingTimers[key];
                }

                if (isTyping) {
                    let displayName =
                        lookupParticipantName(presenceConversationId, userId) ??
                        lookupParticipantName(`${DISCIPLINE_PREFIX}${presenceConversationId}`, userId);
                    store.setTyping({
                        userId,
                        conversationId: presenceConversationId,
                        displayName,
                    });

                    // Защита от потерянного StopTyping: чистим запись, если очередной
                    // StartTyping не подоспеет за TYPING_TIMEOUT_MS.
                    typingTimers[key] = setTimeout(() => {
                        store.clearTyping(userId, presenceConversationId);
                        delete typingTimers[key];
                    }, TYPING_TIMEOUT_MS);
                } else {
                    store.clearTyping(userId, presenceConversationId);
                }
            },
        );

        newConnection.onreconnecting((err) => {
            if (get().connection !== newConnection) {
                return;
            }

            console.warn("PresenceHub reconnecting", err);
            set({ isConnected: false });
        });

        newConnection.onreconnected(() => {
            if (get().connection !== newConnection) {
                return;
            }

            set({ isConnected: true });
            void subscribeWatchedUsers();
        });

        newConnection.onclose((err) => {
            if (get().connection !== newConnection) {
                return;
            }

            if (err) {
                console.warn("PresenceHub closed", err);
            }
            set({ connection: null, isConnected: false });
        });

        try {
            set({ connection: newConnection, isConnected: false });
            await newConnection.start();
            if (get().connection !== newConnection) {
                await newConnection.stop().catch(() => undefined);
                return;
            }
            set({ connection: newConnection, isConnected: true });
            await subscribeWatchedUsers();
        } catch (e) {
            console.error("Failed to connect to PresenceHub", e);
            if (get().connection === newConnection) {
                set({ connection: null, isConnected: false });
            }
        }
    },

    disconnect: async () => {
        const { connection } = get();
        if (connection) {
            await connection.stop();
            set({ connection: null, isConnected: false });
        }
    },

    watchUserPresence: async (userId) => {
        if (!isUuid(userId)) return;

        const previousCount = get().watchedUserRefCounts[userId] ?? 0;
        const shouldSubscribe = previousCount === 0;
        set((state) => ({
            watchedUserRefCounts: {
                ...state.watchedUserRefCounts,
                [userId]: previousCount + 1,
            },
            watchedUserIds: previousCount === 0
                ? [...state.watchedUserIds, userId]
                : state.watchedUserIds,
        }));

        const { connection } = get();
        if (shouldSubscribe && connection?.state === HubConnectionState.Connected) {
            await connection.invoke("SubscribeToUsers", [userId]).catch((error) =>
                warnUnlessConnectionClosingError("Presence subscribe failed", error),
            );
        }
    },

    unwatchUserPresence: (userId) => {
        if (!isUuid(userId)) return;

        const previousCount = get().watchedUserRefCounts[userId] ?? 0;
        const shouldUnsubscribe = previousCount === 1;
        set((state) => ({
            watchedUserRefCounts: previousCount <= 1
                ? Object.fromEntries(
                      Object.entries(state.watchedUserRefCounts).filter(([id]) => id !== userId),
                  )
                : {
                      ...state.watchedUserRefCounts,
                      [userId]: previousCount - 1,
                  },
            watchedUserIds:
                previousCount <= 1
                    ? state.watchedUserIds.filter((id) => id !== userId)
                    : state.watchedUserIds,
        }));

        const { connection } = get();
        if (shouldUnsubscribe && connection?.state === HubConnectionState.Connected) {
            connection.invoke("UnsubscribeFromUsers", [userId]).catch((error) =>
                warnUnlessConnectionClosingError("Presence unsubscribe failed", error),
            );
        }
    },

    setUserPresence: (info) => {
        const normalizedInfo = {
            ...info,
            status: normalizePresenceStatus(info.status),
            platforms: normalizePlatforms(info.platforms),
        };

        set((state) => ({
            presenceByUser: {
                ...state.presenceByUser,
                [normalizedInfo.userId]: normalizedInfo,
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
                warnUnlessConnectionClosingError("Heartbeat failed", e)
            );
        }
    },

    startTyping: (conversationId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            connection.invoke("StartTyping", conversationId).catch((e) =>
                warnUnlessConnectionClosingError("StartTyping failed", e)
            );
        }
    },

    stopTyping: (conversationId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            connection.invoke("StopTyping", conversationId).catch((e) =>
                warnUnlessConnectionClosingError("StopTyping failed", e)
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

type UseConversationTypersOptions = {
    excludeUserId?: string | null;
};

export const useConversationTypers = (
    conversationId: string,
    { excludeUserId }: UseConversationTypersOptions = {},
): TypingUser[] => {
    const typers = usePresenceStore(
        (state) =>
            state.typingByConversation[toPresenceTypingConversationId(conversationId)] ??
            EMPTY_TYPERS,
    );
    return useMemo(
        () => excludeUserId ? typers.filter((typer) => typer.userId !== excludeUserId) : typers,
        [excludeUserId, typers],
    );
};

export const presenceStatusToLabel = (status: PresenceStatus | number | string): string => {
    switch (normalizePresenceStatus(status)) {
        case "Online": return "В сети";
        case "Away": return "Отошёл";
        case "DoNotDisturb": return "Не беспокоить";
        case "Invisible":
        case "Offline": return "Не в сети";
        default: return "";
    }
};
