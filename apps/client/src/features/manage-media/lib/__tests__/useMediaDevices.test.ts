import { act, renderHook, waitFor } from "@testing-library/react-native";
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
                platform: "Win32",
                userAgent:
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0.0.0 Safari/537.36",
                maxTouchPoints: 0,
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
        (navigator.mediaDevices.enumerateDevices as jest.Mock).mockReturnValue(
            new Promise(() => undefined),
        );

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

    it("resolves a saved device label on mount when browser labels are already available", async () => {
        const { result } = renderHook(() => useMediaDevices("audioinput", "stored-mic"));

        await waitFor(() => {
            expect(result.current.options).toEqual([
                { label: "Studio Mic", value: "stored-mic" },
                { label: "Backup Mic", value: "other-mic" },
            ]);
        });
        expect(navigator.mediaDevices.getUserMedia).not.toHaveBeenCalled();
        expect(result.current.selectedValue).toBe("stored-mic");
    });

    it("groups Android browser cameras by facing mode instead of raw camera ids", async () => {
        Object.defineProperty(globalThis, "navigator", {
            configurable: true,
            value: {
                platform: "Linux armv8l",
                userAgent:
                    "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/125.0.0.0 Mobile Safari/537.36",
                maxTouchPoints: 5,
                mediaDevices: {
                    getUserMedia: jest.fn(async () => ({
                        getTracks: () => [{ stop: jest.fn() }],
                    })),
                    enumerateDevices: jest.fn(async () => [
                        {
                            kind: "videoinput",
                            deviceId: "camera-1",
                            label: "camera 1, facing front",
                        },
                        {
                            kind: "videoinput",
                            deviceId: "camera-3",
                            label: "camera 3, facing front",
                        },
                        {
                            kind: "videoinput",
                            deviceId: "camera-2",
                            label: "camera 2, facing back",
                        },
                        {
                            kind: "videoinput",
                            deviceId: "camera-0",
                            label: "camera 0, facing back",
                        },
                    ]),
                },
            },
        });

        const { result } = renderHook(() =>
            useMediaDevices("videoinput", null, { initialFacingMode: "user" }),
        );

        await act(async () => {
            await result.current.requestAccess();
        });

        expect(navigator.mediaDevices.getUserMedia).toHaveBeenCalledWith({
            video: { facingMode: "user" },
        });
        expect(result.current.options).toEqual([
            { label: "Передняя камера", value: "facing:user" },
            { label: "Задняя камера", value: "facing:environment" },
        ]);
        expect(result.current.selectedValue).toBe("facing:user");
    });

    it("does not request microphone permission for unsupported speaker selection", async () => {
        const { result } = renderHook(() => useMediaDevices("audiooutput"));

        await act(async () => {
            await result.current.requestAccess();
        });

        expect(navigator.mediaDevices.getUserMedia).not.toHaveBeenCalled();
        expect(result.current.permissionStatus).toBe("not_found");
    });

    it("sets denied status when microphone permission is blocked", async () => {
        (navigator.mediaDevices.getUserMedia as jest.Mock).mockRejectedValueOnce(
            Object.assign(new Error("denied"), { name: "NotAllowedError" }),
        );

        const { result } = renderHook(() => useMediaDevices("audioinput"));

        await act(async () => {
            await result.current.requestAccess();
        });

        expect(result.current.permissionStatus).toBe("denied");
        expect(result.current.options).toEqual([]);
    });
});
