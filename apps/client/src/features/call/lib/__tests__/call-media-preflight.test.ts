import { Platform } from "react-native";

import {
    clearCachedCallMediaPreflight,
    ensureCallMediaAccess,
    getCachedCallMediaPreflight,
} from "../call-media-preflight";

describe("call-media-preflight", () => {
    const originalOS = Platform.OS;
    const originalNavigator = globalThis.navigator;

    beforeEach(() => {
        clearCachedCallMediaPreflight();
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
        Object.defineProperty(globalThis, "navigator", {
            configurable: true,
            value: {
                mediaDevices: {
                    getUserMedia: jest.fn(async () => ({
                        getTracks: () => [{ stop: jest.fn() }],
                    })),
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
        clearCachedCallMediaPreflight();
        jest.clearAllMocks();
    });

    it("blocks call entry when microphone permission is denied", async () => {
        (navigator.mediaDevices.getUserMedia as jest.Mock).mockRejectedValueOnce(
            Object.assign(new Error("denied"), { name: "NotAllowedError" }),
        );

        const result = await ensureCallMediaAccess({
            callType: "Audio",
            recordingDeviceId: "mic-1",
        });

        expect(result).toEqual({
            ok: false,
            errorMessage: "Не удалось получить доступ к микрофону. Проверьте разрешения браузера.",
        });
        expect(navigator.mediaDevices.getUserMedia).toHaveBeenCalledWith({
            audio: { deviceId: { exact: "mic-1" } },
        });
    });

    it("keeps video call entry allowed when only camera permission is denied", async () => {
        (navigator.mediaDevices.getUserMedia as jest.Mock)
            .mockResolvedValueOnce({ getTracks: () => [{ stop: jest.fn() }] })
            .mockRejectedValueOnce(
                Object.assign(new Error("camera denied"), { name: "NotAllowedError" }),
            );

        const result = await ensureCallMediaAccess({
            callType: "Video",
            webcamFacingMode: "environment",
        });

        expect(result).toEqual({
            ok: true,
            microphoneGranted: true,
            cameraGranted: false,
            audioCaptureOptions: undefined,
            videoCaptureOptions: { facingMode: "environment" },
        });
        expect(navigator.mediaDevices.getUserMedia).toHaveBeenNthCalledWith(2, {
            video: { facingMode: "environment" },
        });
        expect(getCachedCallMediaPreflight({
            callType: "Video",
            webcamFacingMode: "environment",
        })?.cameraGranted).toBe(false);
    });
});
