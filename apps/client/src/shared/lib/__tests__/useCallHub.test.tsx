import { renderHook, waitFor } from "@testing-library/react-native";
import { AppState } from "react-native";

import { useCallHub } from "../useCallHub";

const handlers: Record<string, (...args: any[]) => void> = {};
const mockConnection = {
    state: "Disconnected",
    on: jest.fn((event: string, handler: (...args: any[]) => void) => {
        handlers[event] = handler;
    }),
    off: jest.fn(),
    start: jest.fn(async () => {
        mockConnection.state = "Connected";
    }),
    stop: jest.fn(),
};
const mockHandleIncomingCall = jest.fn();
const mockPlayMessageSound = jest.fn();

jest.mock("@microsoft/signalr", () => ({
    HubConnectionState: {
        Connected: "Connected",
    },
}));

jest.mock("@/shared/lib/signalr", () => ({
    createHubConnection: jest.fn(() => mockConnection),
    HUB_CONNECTION_STATE: {
        Connected: "Connected",
        Connecting: "Connecting",
        Disconnected: "Disconnected",
    },
}));

jest.mock("@/entities/call/model/call-store", () => ({
    useCallStore: (selector: (state: Record<string, jest.Mock>) => unknown) =>
        selector({
            handleIncomingCall: mockHandleIncomingCall,
            handleCallAccepted: jest.fn(),
            handleCallDeclined: jest.fn(),
            handleCallCancelled: jest.fn(),
            handleCallEnded: jest.fn(),
            handleCallParticipantJoined: jest.fn(),
            handleCallParticipantLeft: jest.fn(),
            handleCallDeviceReplaced: jest.fn(),
        }),
}));

jest.mock("@/shared/lib/message-sounds", () => ({
    playMessageSound: (...args: unknown[]) => mockPlayMessageSound(...args),
}));

describe("useCallHub", () => {
    let pagehideHandler: (() => void) | null = null;
    let pageshowHandler: (() => void) | null = null;
    const addWindowEventListener = jest.fn((event: string, handler: () => void) => {
        if (event === "pagehide") pagehideHandler = handler;
        if (event === "pageshow") pageshowHandler = handler;
    });
    const removeWindowEventListener = jest.fn();

    beforeEach(() => {
        jest.clearAllMocks();
        Object.keys(handlers).forEach((key) => delete handlers[key]);
        mockConnection.state = "Disconnected";
        pagehideHandler = null;
        pageshowHandler = null;
        Object.defineProperty(globalThis, "window", {
            configurable: true,
            value: {
                addEventListener: addWindowEventListener,
                removeEventListener: removeWindowEventListener,
            },
        });
        jest.spyOn(AppState, "addEventListener").mockReturnValue({ remove: jest.fn() });
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it("handles ringing direct calls as incoming calls", async () => {
        renderHook(() => useCallHub());
        await waitFor(() => expect(handlers.IncomingCall).toEqual(expect.any(Function)));

        handlers.IncomingCall({
            id: "call-1",
            conversationId: "direct-1",
            status: "Ringing",
        });

        expect(mockHandleIncomingCall).toHaveBeenCalledWith(
            expect.objectContaining({ id: "call-1" }),
        );
        expect(mockPlayMessageSound).toHaveBeenCalledWith("receive", {
            conversationId: "direct-1",
        });
    });

    it("ignores already active discipline group calls so joined users do not hear ringtone", async () => {
        renderHook(() => useCallHub());
        await waitFor(() => expect(handlers.IncomingCall).toEqual(expect.any(Function)));

        handlers.IncomingCall({
            id: "call-1",
            conversationId: "discipline:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            status: "Active",
        });

        expect(mockHandleIncomingCall).not.toHaveBeenCalled();
        expect(mockPlayMessageSound).not.toHaveBeenCalled();
    });

    it("stops the connection before bfcache and reconnects when restored", async () => {
        renderHook(() => useCallHub());
        await waitFor(() => expect(pagehideHandler).toEqual(expect.any(Function)));
        mockConnection.start.mockClear();
        mockConnection.state = "Disconnected";

        pagehideHandler?.();
        pageshowHandler?.();

        expect(mockConnection.stop).toHaveBeenCalledTimes(1);
        expect(mockConnection.start).toHaveBeenCalledTimes(1);
        expect(addWindowEventListener).not.toHaveBeenCalledWith(
            "beforeunload",
            expect.any(Function),
        );
    });
});
