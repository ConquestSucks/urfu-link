import { useCallback, useEffect, useState } from "react";
import { Platform } from "react-native";

type DeviceKind = "audioinput" | "videoinput" | "audiooutput";
type PermissionStatus = "idle" | "granted" | "denied" | "not_found" | "busy" | "error";

const savedDeviceOption = (deviceId: string) => ({
    label: "Сохраненное устройство",
    value: deviceId,
});

const fallbackDeviceLabel = (kind: DeviceKind, index: number) => {
    if (kind === "videoinput") {
        return `Камера ${index + 1}`;
    }

    if (kind === "audiooutput") {
        return `Динамики ${index + 1}`;
    }

    return `Микрофон ${index + 1}`;
};

export const useMediaDevices = (kind: DeviceKind, initialDeviceId?: string | null) => {
    const [options, setOptions] = useState<{ label: string; value: string }[]>(() =>
        initialDeviceId ? [savedDeviceOption(initialDeviceId)] : [],
    );
    const [selectedValue, setSelectedValue] = useState<string | number>(initialDeviceId ?? "");
    const [isLoading, setIsLoading] = useState(false);
    const [permissionStatus, setPermissionStatus] = useState<PermissionStatus>("idle");

    // Sync initial value once it arrives (e.g. from profile query)
    useEffect(() => {
        if (!initialDeviceId) {
            return;
        }

        setSelectedValue((current) => current || initialDeviceId);
        setOptions((current) =>
            current.some((option) => option.value === initialDeviceId)
                ? current
                : [savedDeviceOption(initialDeviceId), ...current],
        );
    }, [initialDeviceId]);

    const requestAccess = useCallback(async () => {
        if (permissionStatus === "granted") return;
        setIsLoading(true);
        try {
            if (Platform.OS === "web") {
                const constraints = {
                    audio: kind === "audioinput" || kind === "audiooutput",
                    video: kind === "videoinput",
                };
                const stream = await navigator.mediaDevices.getUserMedia(constraints);
                const allDevices = await navigator.mediaDevices.enumerateDevices();
                const filteredDevices = allDevices
                    .filter((device) => device.kind === kind)
                    .map((device, index) => ({
                        label: device.label || fallbackDeviceLabel(kind, index),
                        value: device.deviceId,
                    }));
                const nextOptions =
                    initialDeviceId &&
                    !filteredDevices.some((device) => device.value === initialDeviceId)
                        ? [savedDeviceOption(initialDeviceId), ...filteredDevices]
                        : filteredDevices;
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
    }, [kind, permissionStatus, initialDeviceId]);

    return {
        options,
        selectedValue,
        setSelectedValue,
        isLoading,
        permissionStatus,
        requestAccess,
    };
};
