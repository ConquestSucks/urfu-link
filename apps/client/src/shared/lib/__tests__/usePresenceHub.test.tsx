import { act, renderHook } from "@testing-library/react-native";
import { AppState, Platform, type AppStateStatus } from "react-native";

import { notifyPresenceDisconnect } from "@/shared/lib/signalr";
import { usePresenceHub } from "../usePresenceHub";

const mockConnect = jest.fn(async (): Promise<void> => undefined);
const mockDisconnect = jest.fn(async (): Promise<void> => undefined);
const mockSendHeartbeat = jest.fn();

jest.mock("@/entities/presence", () => ({
    usePresenceStore: () => ({
        connect: mockConnect,
        disconnect: mockDisconnect,
        sendHeartbeat: mockSendHeartbeat,
    }),
}));

jest.mock("@/shared/lib/signalr", () => ({
    notifyPresenceDisconnect: jest.fn(),
}));

describe("usePresenceHub", () => {
    let appStateHandler: ((state: AppStateStatus) => void) | null = null;
    let pagehideHandler: (() => void) | null = null;
    let beforeUnloadHandler: (() => void) | null = null;
    const remove = jest.fn();
    const addWindowEventListener = jest.fn((event: string, handler: () => void) => {
        if (event === "pagehide") pagehideHandler = handler;
        if (event === "beforeunload") beforeUnloadHandler = handler;
    });
    const removeWindowEventListener = jest.fn();
    const setPlatformOS = (os: typeof Platform.OS) => {
        Object.defineProperty(Platform, "OS", {
            configurable: true,
            get: () => os,
        });
    };

    beforeEach(() => {
        jest.useFakeTimers();
        jest.clearAllMocks();
        setPlatformOS("ios");
        appStateHandler = null;
        pagehideHandler = null;
        beforeUnloadHandler = null;
        Object.defineProperty(globalThis, "window", {
            configurable: true,
            value: {
                addEventListener: addWindowEventListener,
                removeEventListener: removeWindowEventListener,
            },
        });
        jest.spyOn(AppState, "addEventListener").mockImplementation((_event, handler) => {
            appStateHandler = handler;
            return { remove };
        });
    });

    afterEach(() => {
        jest.runOnlyPendingTimers();
        jest.useRealTimers();
        jest.restoreAllMocks();
    });

    it("disconnects immediately when the app leaves the foreground", async () => {
        renderHook(() => usePresenceHub());

        await act(async () => {
            await Promise.resolve();
        });
        mockDisconnect.mockClear();

        await act(async () => {
            appStateHandler?.("background");
        });

        expect(mockDisconnect).toHaveBeenCalledTimes(1);
    });

    it("keeps web presence connected when the tab is merely hidden", async () => {
        setPlatformOS("web");
        renderHook(() => usePresenceHub());

        await act(async () => {
            await Promise.resolve();
        });
        mockDisconnect.mockClear();

        await act(async () => {
            appStateHandler?.("background");
        });

        expect(mockDisconnect).not.toHaveBeenCalled();
    });

    it("reconnects and resumes heartbeat when the app returns to foreground", async () => {
        renderHook(() => usePresenceHub());

        await act(async () => {
            await Promise.resolve();
        });

        await act(async () => {
            appStateHandler?.("background");
        });
        mockConnect.mockClear();
        mockSendHeartbeat.mockClear();

        await act(async () => {
            appStateHandler?.("active");
            await Promise.resolve();
        });

        expect(mockConnect).toHaveBeenCalledTimes(1);
        expect(mockSendHeartbeat).toHaveBeenCalledTimes(1);
    });

    it("waits for the foreground disconnect to finish before reconnecting", async () => {
        let finishDisconnect!: () => void;
        mockDisconnect.mockImplementationOnce(
            () => new Promise<void>((resolve) => {
                finishDisconnect = resolve;
            }),
        );

        renderHook(() => usePresenceHub());

        await act(async () => {
            await Promise.resolve();
        });
        mockConnect.mockClear();

        await act(async () => {
            appStateHandler?.("background");
            appStateHandler?.("active");
            await Promise.resolve();
        });

        expect(mockDisconnect).toHaveBeenCalledTimes(1);
        expect(mockConnect).not.toHaveBeenCalled();

        await act(async () => {
            finishDisconnect();
            await Promise.resolve();
        });

        expect(mockConnect).toHaveBeenCalledTimes(1);
    });

    it("sends a keepalive disconnect when the page is closing", async () => {
        renderHook(() => usePresenceHub());

        await act(async () => {
            await Promise.resolve();
        });
        mockDisconnect.mockClear();

        act(() => {
            pagehideHandler?.();
        });

        expect(mockDisconnect).toHaveBeenCalledTimes(1);
        expect(notifyPresenceDisconnect).toHaveBeenCalledTimes(1);
        expect(beforeUnloadHandler).not.toBeNull();
    });

    it("queues the keepalive disconnect before stopping the hub on page close", async () => {
        const order: string[] = [];
        (notifyPresenceDisconnect as jest.Mock).mockImplementationOnce(() => {
            order.push("notify");
        });
        mockDisconnect.mockImplementationOnce(async () => {
            order.push("disconnect");
        });

        renderHook(() => usePresenceHub());

        await act(async () => {
            await Promise.resolve();
        });

        act(() => {
            pagehideHandler?.();
        });

        expect(order).toEqual(["notify", "disconnect"]);
    });
});
