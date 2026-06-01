import { Platform } from "react-native";
import type { CallType, UserSoundVideo } from "@urfu-link/api-client";

export type WebcamFacingMode = "user" | "environment";

export type CallMediaPreflightInput = {
    callType: CallType;
    recordingDeviceId?: string | null;
    webcamDeviceId?: string | null;
    webcamFacingMode?: WebcamFacingMode | null;
    soundVideo?: Partial<UserSoundVideo> | null;
};

export type CallMediaPreflightSuccess = {
    ok: true;
    microphoneGranted: true;
    cameraGranted: boolean;
    audioCaptureOptions?: { deviceId: string };
    videoCaptureOptions?: { deviceId: string } | { facingMode: WebcamFacingMode };
};

export type CallMediaPreflightFailure = {
    ok: false;
    errorMessage: string;
};

export type CallMediaPreflightResult =
    | CallMediaPreflightSuccess
    | CallMediaPreflightFailure;

const microphoneErrorMessage =
    "Не удалось получить доступ к микрофону. Проверьте разрешения браузера.";

const cacheTtlMs = 5 * 60 * 1000;

let cachedPreflight:
    | {
          key: string;
          result: CallMediaPreflightSuccess;
          checkedAt: number;
      }
    | null = null;

const stopStream = (stream: MediaStream) => {
    stream.getTracks().forEach((track) => track.stop());
};

const getMediaDevices = () => {
    if (Platform.OS !== "web" || typeof navigator === "undefined") {
        return null;
    }

    const mediaDevices = navigator.mediaDevices as
        | (MediaDevices & { getUserMedia?: MediaDevices["getUserMedia"] })
        | undefined;

    return typeof mediaDevices?.getUserMedia === "function" ? mediaDevices : null;
};

const resolveInput = (input: CallMediaPreflightInput) => ({
    callType: input.callType,
    recordingDeviceId: input.recordingDeviceId ?? input.soundVideo?.recordingDeviceId ?? null,
    webcamDeviceId: input.webcamDeviceId ?? input.soundVideo?.webcamDeviceId ?? null,
    webcamFacingMode: input.webcamFacingMode ?? input.soundVideo?.webcamFacingMode ?? null,
});

const getCacheKey = (input: CallMediaPreflightInput) => {
    const resolved = resolveInput(input);
    return JSON.stringify([
        resolved.callType,
        resolved.recordingDeviceId,
        resolved.webcamDeviceId,
        resolved.webcamFacingMode,
    ]);
};

const getAudioCaptureOptions = (recordingDeviceId?: string | null) =>
    recordingDeviceId ? { deviceId: recordingDeviceId } : undefined;

const getAudioUserMediaConstraints = (recordingDeviceId?: string | null) => ({
    audio: recordingDeviceId
        ? {
              deviceId: { exact: recordingDeviceId },
          }
        : true,
});

const getVideoCaptureOptions = (
    webcamDeviceId?: string | null,
    webcamFacingMode?: WebcamFacingMode | null,
) =>
    webcamDeviceId
        ? { deviceId: webcamDeviceId }
        : { facingMode: webcamFacingMode ?? "user" as const };

const getVideoUserMediaConstraints = (
    webcamDeviceId?: string | null,
    webcamFacingMode?: WebcamFacingMode | null,
) => ({
    video: webcamDeviceId
        ? {
              deviceId: { exact: webcamDeviceId },
          }
        : {
              facingMode: webcamFacingMode ?? "user",
          },
});

export const clearCachedCallMediaPreflight = () => {
    cachedPreflight = null;
};

export const getCachedCallMediaPreflight = (
    input: CallMediaPreflightInput,
): CallMediaPreflightSuccess | null => {
    if (!cachedPreflight || cachedPreflight.key !== getCacheKey(input)) {
        return null;
    }

    if (Date.now() - cachedPreflight.checkedAt > cacheTtlMs) {
        cachedPreflight = null;
        return null;
    }

    return cachedPreflight.result;
};

export const ensureCallMediaAccess = async (
    input: CallMediaPreflightInput,
): Promise<CallMediaPreflightResult> => {
    const mediaDevices = getMediaDevices();
    const resolved = resolveInput(input);
    const audioCaptureOptions = getAudioCaptureOptions(resolved.recordingDeviceId);
    const videoCaptureOptions =
        resolved.callType === "Video"
            ? getVideoCaptureOptions(resolved.webcamDeviceId, resolved.webcamFacingMode)
            : undefined;

    if (!mediaDevices) {
        return {
            ok: true,
            microphoneGranted: true,
            cameraGranted: resolved.callType === "Video",
            audioCaptureOptions,
            videoCaptureOptions,
        };
    }

    try {
        const audioStream = await mediaDevices.getUserMedia(
            getAudioUserMediaConstraints(resolved.recordingDeviceId),
        );
        stopStream(audioStream);
    } catch {
        return {
            ok: false,
            errorMessage: microphoneErrorMessage,
        };
    }

    let cameraGranted = false;
    if (resolved.callType === "Video") {
        try {
            const videoStream = await mediaDevices.getUserMedia(
                getVideoUserMediaConstraints(resolved.webcamDeviceId, resolved.webcamFacingMode),
            );
            stopStream(videoStream);
            cameraGranted = true;
        } catch {
            cameraGranted = false;
        }
    }

    const result: CallMediaPreflightSuccess = {
        ok: true,
        microphoneGranted: true,
        cameraGranted,
        audioCaptureOptions,
        videoCaptureOptions,
    };

    cachedPreflight = {
        key: getCacheKey(input),
        result,
        checkedAt: Date.now(),
    };

    return result;
};
