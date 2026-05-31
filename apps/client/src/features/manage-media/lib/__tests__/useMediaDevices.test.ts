import { act, renderHook } from "@testing-library/react-native";
import { Platform } from "react-native";

import { useMediaDevices } from "../useMediaDevices";

describe("useMediaDevices", () => {
    const originalOS = Platform.OS;
    const originalNavigator = globalThis.navigator;

    beforeEach(() => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
        Object.defineProperty(globalThis, "navigator", {
            configurable: true,
            value: {
                mediaDevices: {
                    getUserMedia: jest.fn(async () => ({
                        getTracks: () => [{ stop: jest.fn() }],
                    })),
                    enumerateDevices: jest.fn(async () => [
                        {
                            kind: "audioinput",
                            deviceId: "stored-mic",
                            label: "Studio Mic",
                        },
                        {
                            kind: "audioinput",
                            deviceId: "other-mic",
                            label: "Backup Mic",
                        },
                    ]),
                },
            },
        });
    });

    afterEach(() => {
        Object.defineProperty(Platform, "OS", {
            configurable: true,
            value: originalOS,
        });
        Object.defineProperty(globalThis, "navigator", {
            configurable: true,
            value: originalNavigator,
        });
        jest.clearAllMocks();
    });

    it("shows a saved device before browser device labels are available", () => {
        const { result } = renderHook(() => useMediaDevices("audioinput", "stored-mic"));

        expect(result.current.selectedValue).toBe("stored-mic");
        expect(result.current.options).toEqual([
            { label: "Сохраненное устройство", value: "stored-mic" },
        ]);
    });

    it("keeps the saved device selected after enumerating devices", async () => {
        const { result } = renderHook(() => useMediaDevices("audioinput", "stored-mic"));

        await act(async () => {
            await result.current.requestAccess();
        });

        expect(result.current.selectedValue).toBe("stored-mic");
        expect(result.current.options).toEqual([
            { label: "Studio Mic", value: "stored-mic" },
            { label: "Backup Mic", value: "other-mic" },
        ]);
    });
});
