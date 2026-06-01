import { renderHook } from "@testing-library/react-native";
import { AppState } from "react-native";

import { useChatHub } from "../useChatHub";

const mockConnect = jest.fn();
const mockDisconnect = jest.fn();

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: () => ({
        connect: mockConnect,
        disconnect: mockDisconnect,
    }),
}));

describe("useChatHub", () => {
    let pagehideHandler: (() => void) | null = null;
    let pageshowHandler: (() => void) | null = null;
    const remove = jest.fn();
    const addWindowEventListener = jest.fn((event: string, handler: () => void) => {
        if (event === "pagehide") pagehideHandler = handler;
        if (event === "pageshow") pageshowHandler = handler;
    });
    const removeWindowEventListener = jest.fn();

    beforeEach(() => {
        jest.clearAllMocks();
        pagehideHandler = null;
        pageshowHandler = null;
        Object.defineProperty(globalThis, "window", {
            configurable: true,
            value: {
                addEventListener: addWindowEventListener,
                removeEventListener: removeWindowEventListener,
            },
        });
        jest.spyOn(AppState, "addEventListener").mockReturnValue({ remove });
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it("disconnects before bfcache and reconnects on restore", () => {
        renderHook(() => useChatHub());
        mockConnect.mockClear();

        pagehideHandler?.();
        pageshowHandler?.();

        expect(mockDisconnect).toHaveBeenCalledTimes(1);
        expect(mockConnect).toHaveBeenCalledTimes(1);
        expect(addWindowEventListener).not.toHaveBeenCalledWith(
            "beforeunload",
            expect.any(Function),
        );
    });

    it("does not require browser page lifecycle APIs on native", () => {
        Object.defineProperty(globalThis, "window", {
            configurable: true,
            value: {},
        });

        expect(() => renderHook(() => useChatHub())).not.toThrow();
        expect(mockConnect).toHaveBeenCalledTimes(1);
    });
});
