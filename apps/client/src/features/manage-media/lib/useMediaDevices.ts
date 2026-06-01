import { useCallback, useEffect, useState } from "react";
import { Platform } from "react-native";
import {
    getCurrentBrowserDeviceInfo,
    isMobileBrowserDevice,
} from "@/shared/lib/browser-device";

type DeviceKind = "audioinput" | "videoinput" | "audiooutput";
type PermissionStatus = "idle" | "granted" | "denied" | "not_found" | "busy" | "error";
type WebcamFacingMode = "user" | "environment";
type DeviceOption = { label: string; value: string };

type UseMediaDevicesOptions = {
    initialFacingMode?: WebcamFacingMode | null;
};

export const defaultDeviceValue = "default";
export const frontCameraValue = "facing:user";
export const backCameraValue = "facing:environment";

export const toFacingModeValue = (mode: WebcamFacingMode) =>
    mode === "environment" ? backCameraValue : frontCameraValue;

export const parseFacingModeValue = (value: string | number): WebcamFacingMode | null => {
    if (value === frontCameraValue) return "user";
    if (value === backCameraValue) return "environment";
    return null;
};

const savedDeviceOption = (deviceId: string) => ({
    label: "Сохраненное устройство",
    value: deviceId,
});

const defaultDeviceOption = (kind: DeviceKind) => ({
    label: kind === "audioinput" ? "Системный микрофон" : "Системное устройство",
    value: defaultDeviceValue,
});

const mobileCameraOptions: DeviceOption[] = [
    { label: "Передняя камера", value: frontCameraValue },
    { label: "Задняя камера", value: backCameraValue },
];

const fallbackDeviceLabel = (kind: DeviceKind, index: number) => {
    if (kind === "videoinput") {
        return `Камера ${index + 1}`;
    }

    if (kind === "audiooutput") {
        return `Динамики ${index + 1}`;
    }

    return `Микрофон ${index + 1}`;
};

const mapDeviceOptions = (devices: MediaDeviceInfo[], kind: DeviceKind) =>
    devices
        .filter((device) => device.kind === kind)
        .map((device, index) => ({
            label: device.label || fallbackDeviceLabel(kind, index),
            value: device.deviceId || defaultDeviceValue,
        }));

const mergeSavedDeviceOption = (
    options: DeviceOption[],
    deviceId?: string | null,
) =>
    deviceId && !options.some((device) => device.value === deviceId)
        ? [savedDeviceOption(deviceId), ...options]
        : options;

export const hasAudioOutputSelection = () => {
    if (Platform.OS !== "web" || typeof document === "undefined") {
        return false;
    }

    const audio = document.createElement("audio") as HTMLMediaElement & {
        setSinkId?: (sinkId: string) => Promise<void>;
    };

    return typeof audio.setSinkId === "function";
};

const isMobileBrowser = () =>
    Platform.OS === "web" && isMobileBrowserDevice(getCurrentBrowserDeviceInfo());

const shouldUseFacingModeOptions = (kind: DeviceKind) =>
    kind === "videoinput" && isMobileBrowser();

const resolveInitialSelectedValue = (
    kind: DeviceKind,
    initialDeviceId?: string | null,
    initialFacingMode?: WebcamFacingMode | null,
) => {
    if (shouldUseFacingModeOptions(kind)) {
        return toFacingModeValue(initialFacingMode ?? "user");
    }

    return initialDeviceId ?? "";
};

const buildInitialOptions = (
    kind: DeviceKind,
    initialDeviceId?: string | null,
    initialFacingMode?: WebcamFacingMode | null,
) => {
    if (shouldUseFacingModeOptions(kind)) {
        return mobileCameraOptions;
    }

    return initialDeviceId ? [savedDeviceOption(initialDeviceId)] : [];
};

export const useMediaDevices = (
    kind: DeviceKind,
    initialDeviceId?: string | null,
    hookOptions: UseMediaDevicesOptions = {},
) => {
    const { initialFacingMode } = hookOptions;
    const [options, setOptions] = useState<DeviceOption[]>(() =>
        buildInitialOptions(kind, initialDeviceId, initialFacingMode),
    );
    const [selectedValue, setSelectedValue] = useState<string | number>(() =>
        resolveInitialSelectedValue(kind, initialDeviceId, initialFacingMode),
    );
    const [isLoading, setIsLoading] = useState(false);
    const [permissionStatus, setPermissionStatus] = useState<PermissionStatus>("idle");

    // Sync initial value once it arrives (e.g. from profile query)
    useEffect(() => {
        if (shouldUseFacingModeOptions(kind)) {
            setOptions(mobileCameraOptions);
            setSelectedValue((current) =>
                parseFacingModeValue(current) ? current : toFacingModeValue(initialFacingMode ?? "user"),
            );
            return;
        }

        if (!initialDeviceId) {
            return;
        }

        setSelectedValue((current) => current || initialDeviceId);
        setOptions((current) =>
            current.some((option) => option.value === initialDeviceId)
                ? current
                : [savedDeviceOption(initialDeviceId), ...current],
        );
    }, [initialDeviceId, initialFacingMode, kind]);

    useEffect(() => {
        if (
            !initialDeviceId ||
            Platform.OS !== "web" ||
            !navigator.mediaDevices?.enumerateDevices
        ) {
            return;
        }

        if (shouldUseFacingModeOptions(kind)) {
            return;
        }

        let cancelled = false;
        void navigator.mediaDevices
            .enumerateDevices()
            .then((devices) => {
                if (cancelled) return;
                const nextOptions = mergeSavedDeviceOption(
                    mapDeviceOptions(devices, kind),
                    initialDeviceId,
                );
                setOptions(nextOptions);
                setSelectedValue((current) =>
                    nextOptions.some((device) => device.value === current)
                        ? current
                        : initialDeviceId,
                );
            })
            .catch(() => undefined);

        return () => {
            cancelled = true;
        };
    }, [kind, initialDeviceId]);

    const requestAccess = useCallback(async () => {
        if (permissionStatus === "granted") return;
        setIsLoading(true);
        try {
            if (Platform.OS === "web") {
                if (shouldUseFacingModeOptions(kind)) {
                    const facingMode = parseFacingModeValue(selectedValue) ?? initialFacingMode ?? "user";
                    const stream = await navigator.mediaDevices.getUserMedia({
                        video: { facingMode },
                    });
                    setOptions(mobileCameraOptions);
                    setSelectedValue(toFacingModeValue(facingMode));
                    setPermissionStatus("granted");
                    stream.getTracks().forEach((track) => track.stop());
                    return;
                }

                if (kind === "audiooutput" && !hasAudioOutputSelection()) {
                    setOptions([]);
                    setPermissionStatus("not_found");
                    return;
                }

                const constraints = {
                    audio: kind === "audioinput",
                    video: kind === "videoinput",
                };
                const stream = await navigator.mediaDevices.getUserMedia(constraints);
                const allDevices = await navigator.mediaDevices.enumerateDevices();
                const mappedOptions = mapDeviceOptions(allDevices, kind);
                const nextOptions = mergeSavedDeviceOption(
                    mappedOptions.length > 0 ? mappedOptions : [defaultDeviceOption(kind)],
                    initialDeviceId,
                );
                setOptions(nextOptions);
                // Pre-select stored device if available, else first device
                setSelectedValue((current) => {
                    if (
                        initialDeviceId &&
                        nextOptions.some((device) => device.value === initialDeviceId)
                    ) {
                        return initialDeviceId;
                    }

                    if (current && nextOptions.some((device) => device.value === current)) {
                        return current;
                    }

                    return nextOptions[0]?.value ?? current;
                });
                setPermissionStatus("granted");
                stream.getTracks().forEach((track) => track.stop());
            } else {
                setTimeout(() => {
                    setOptions([{ label: "Системное устройство", value: "default" }]);
                    setSelectedValue(initialDeviceId ?? "default");
                    setPermissionStatus("granted");
                    setIsLoading(false);
                }, 800);
            }
        } catch (error: any) {
            switch (error.name) {
                case "NotFoundError":
                case "DevicesNotFoundError":
                    setPermissionStatus("not_found");
                    break;
                case "NotReadableError":
                case "TrackStartError":
                    setPermissionStatus("busy");
                    break;
                case "NotAllowedError":
                case "PermissionDeniedError":
                    setPermissionStatus("denied");
                    break;
                default:
                    setPermissionStatus("error");
            }
        } finally {
            setIsLoading(false);
        }
    }, [kind, permissionStatus, initialDeviceId, initialFacingMode, selectedValue]);

    return {
        options,
        selectedValue,
        setSelectedValue,
        isLoading,
        permissionStatus,
        requestAccess,
    };
};
