import { act, renderHook } from "@testing-library/react-native";

import { apiClient } from "@/shared/lib/api";
import { createHubConnection } from "@/shared/lib/signalr";
import {
    presenceStatusToLabel,
    toPresenceTypingConversationId,
    useConversationTypers,
    usePresenceStore,
} from "../presence-store";

const peerUserId = "11111111-1111-4111-8111-111111111111";

const handlers: Record<string, (...args: unknown[]) => void> = {};
const connection = {
    state: "Disconnected",
    on: jest.fn((event: string, handler: (...args: unknown[]) => void) => {
        handlers[event] = handler;
    }),
    onreconnecting: jest.fn((handler: (...args: unknown[]) => void) => {
        handlers.reconnecting = handler;
    }),
    onreconnected: jest.fn((handler: (...args: unknown[]) => void) => {
        handlers.reconnected = handler;
    }),
    start: jest.fn(async () => {
        connection.state = "Connected";
    }),
    stop: jest.fn(async () => {
        connection.state = "Disconnected";
    }),
    invoke: jest.fn(async () => undefined),
};

jest.mock("@microsoft/signalr", () => ({
    HubConnectionState: {
        Connected: "Connected",
        Disconnected: "Disconnected",
    },
}));

jest.mock("@/shared/lib/signalr", () => ({
    createHubConnection: jest.fn(),
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        presence: {
            getUserPresence: jest.fn(),
        },
    },
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    lookupParticipantName: jest.fn(),
    useParticipantsStore: {
        getState: () => ({
            load: jest.fn(async () => []),
        }),
    },
}));

describe("presence store subscriptions", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        connection.state = "Disconnected";
        Object.keys(handlers).forEach((key) => delete handlers[key]);
        (createHubConnection as jest.Mock).mockReturnValue(connection);
        (apiClient.presence.getUserPresence as jest.Mock).mockResolvedValue({
            userId: peerUserId,
            status: "Online",
            platforms: ["Web"],
            lastSeenAt: null,
        });
        usePresenceStore.setState({
            connection: null,
            isConnected: false,
            presenceByUser: {},
            typingByConversation: {},
            watchedUserIds: [],
            watchedUserRefCounts: {},
        });
    });

    it("subscribes when the hub is connected and waits for the hub snapshot", async () => {
        await act(async () => {
            await usePresenceStore.getState().connect();
            await usePresenceStore.getState().watchUserPresence(peerUserId);
        });

        expect(apiClient.presence.getUserPresence).not.toHaveBeenCalled();
        expect(connection.invoke).toHaveBeenCalledWith("SubscribeToUsers", [peerUserId]);
    });

    it("subscribes watched users after a later hub connection", async () => {
        await act(async () => {
            await usePresenceStore.getState().watchUserPresence(peerUserId);
        });

        expect(connection.invoke).not.toHaveBeenCalled();

        await act(async () => {
            await usePresenceStore.getState().connect();
        });

        expect(connection.invoke).toHaveBeenCalledWith("SubscribeToUsers", [peerUserId]);
    });

    it("ignores non-uuid presence ids without calling the api or hub", async () => {
        await act(async () => {
            await usePresenceStore.getState().connect();
            await usePresenceStore.getState().watchUserPresence("not-a-user-id");
        });

        expect(apiClient.presence.getUserPresence).not.toHaveBeenCalled();
        expect(connection.invoke).not.toHaveBeenCalledWith(
            "SubscribeToUsers",
            ["not-a-user-id"],
        );
        expect(usePresenceStore.getState().watchedUserIds).toEqual([]);
    });

    it("maps presence hub events into presence info", async () => {
        await act(async () => {
            await usePresenceStore.getState().connect();
        });

        act(() => {
            handlers.UserPresenceChanged(
                peerUserId,
                "Away",
                ["Web"],
                "2026-05-24T10:00:00.000Z",
            );
        });

        expect(usePresenceStore.getState().presenceByUser[peerUserId]).toEqual({
            userId: peerUserId,
            status: "Away",
            platforms: ["Web"],
            lastSeenAt: "2026-05-24T10:00:00.000Z",
        });
    });

    it("normalizes numeric SignalR enum payloads into string presence info", async () => {
        await act(async () => {
            await usePresenceStore.getState().connect();
        });

        act(() => {
            handlers.UserPresenceChanged(
                peerUserId,
                0,
                [1],
                "2026-05-24T10:00:00.000Z",
            );
        });

        expect(usePresenceStore.getState().presenceByUser[peerUserId]).toEqual({
            userId: peerUserId,
            status: "Online",
            platforms: ["Web"],
            lastSeenAt: "2026-05-24T10:00:00.000Z",
        });
        expect(presenceStatusToLabel(0)).toBe("В сети");
    });

    it("resubscribes watched users after reconnect", async () => {
        await act(async () => {
            await usePresenceStore.getState().watchUserPresence(peerUserId);
            await usePresenceStore.getState().connect();
        });
        connection.invoke.mockClear();

        act(() => {
            handlers.reconnected();
        });

        expect(connection.invoke).toHaveBeenCalledWith("SubscribeToUsers", [peerUserId]);
        expect(usePresenceStore.getState().isConnected).toBe(true);
    });

    it("keeps a shared user subscription until every watcher releases it", async () => {
        await act(async () => {
            await usePresenceStore.getState().connect();
        });
        connection.invoke.mockClear();

        await act(async () => {
            await usePresenceStore.getState().watchUserPresence(peerUserId);
            await usePresenceStore.getState().watchUserPresence(peerUserId);
        });

        expect(connection.invoke).toHaveBeenCalledTimes(1);
        expect(connection.invoke).toHaveBeenCalledWith("SubscribeToUsers", [peerUserId]);

        act(() => {
            usePresenceStore.getState().unwatchUserPresence(peerUserId);
        });

        expect(usePresenceStore.getState().watchedUserIds).toEqual([peerUserId]);
        expect(connection.invoke).not.toHaveBeenCalledWith("UnsubscribeFromUsers", [peerUserId]);

        act(() => {
            usePresenceStore.getState().unwatchUserPresence(peerUserId);
        });

        expect(usePresenceStore.getState().watchedUserIds).toEqual([]);
        expect(connection.invoke).toHaveBeenCalledWith("UnsubscribeFromUsers", [peerUserId]);
    });

    it("does not warn when subscribe is canceled by a closing hub connection", async () => {
        const warnSpy = jest.spyOn(console, "warn").mockImplementation(() => undefined);

        await act(async () => {
            await usePresenceStore.getState().connect();
        });
        connection.invoke.mockClear();
        connection.invoke.mockRejectedValueOnce(
            new Error("Invocation canceled due to the underlying connection being closed."),
        );

        await act(async () => {
            await usePresenceStore.getState().watchUserPresence(peerUserId);
        });

        expect(connection.invoke).toHaveBeenCalledWith("SubscribeToUsers", [peerUserId]);
        expect(warnSpy).not.toHaveBeenCalled();

        warnSpy.mockRestore();
    });

    it("does not warn when unsubscribe is canceled by a closing hub connection", async () => {
        const warnSpy = jest.spyOn(console, "warn").mockImplementation(() => undefined);

        await act(async () => {
            await usePresenceStore.getState().connect();
            await usePresenceStore.getState().watchUserPresence(peerUserId);
        });
        connection.invoke.mockClear();
        connection.invoke.mockRejectedValueOnce(
            new Error("Invocation canceled due to the underlying connection being closed."),
        );

        act(() => {
            usePresenceStore.getState().unwatchUserPresence(peerUserId);
        });
        await act(async () => {
            await Promise.resolve();
        });

        expect(connection.invoke).toHaveBeenCalledWith("UnsubscribeFromUsers", [peerUserId]);
        expect(warnSpy).not.toHaveBeenCalled();

        warnSpy.mockRestore();
    });

    it("reads typing events by the presence-side conversation id", async () => {
        const directConversationId = "d39b2933cccdd8b2812a2b8f401fb2a9d9f6abcd";
        const presenceConversationId = toPresenceTypingConversationId(directConversationId);

        await act(async () => {
            await usePresenceStore.getState().connect();
        });

        act(() => {
            handlers.UserTyping(presenceConversationId, peerUserId, true);
        });

        const { result } = renderHook(() => useConversationTypers(directConversationId));
        expect(result.current).toEqual([
            expect.objectContaining({
                conversationId: presenceConversationId,
                userId: peerUserId,
            }),
        ]);

        act(() => {
            handlers.UserTyping(presenceConversationId, peerUserId, false);
        });
    });

    it("can exclude typing events for the current user", async () => {
        const directConversationId = "d39b2933cccdd8b2812a2b8f401fb2a9d9f6abcd";
        const presenceConversationId = toPresenceTypingConversationId(directConversationId);

        await act(async () => {
            await usePresenceStore.getState().connect();
        });

        act(() => {
            handlers.UserTyping(presenceConversationId, peerUserId, true);
        });

        const { result } = renderHook(() =>
            useConversationTypers(directConversationId, { excludeUserId: peerUserId }),
        );
        expect(result.current).toEqual([]);

        act(() => {
            handlers.UserTyping(presenceConversationId, peerUserId, false);
        });
    });
});
