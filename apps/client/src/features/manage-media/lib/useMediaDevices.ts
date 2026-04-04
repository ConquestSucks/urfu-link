import { useCallback, useEffect, useState } from "react";
import { Platform } from "react-native";

type DeviceKind = "audioinput" | "videoinput" | "audiooutput";
type PermissionStatus = "idle" | "granted" | "denied" | "not_found" | "busy" | "error";

export const useMediaDevices = (kind: DeviceKind, initialDeviceId?: string | null) => {
    const [options, setOptions] = useState<{ label: string; value: string }[]>([]);
    const [selectedValue, setSelectedValue] = useState<string | number>(initialDeviceId ?? "");
    const [isLoading, setIsLoading] = useState(false);
    const [permissionStatus, setPermissionStatus] = useState<PermissionStatus>("idle");

    // Sync initial value once it arrives (e.g. from profile query)
    useEffect(() => {
        if (initialDeviceId && selectedValue === "") {
            setSelectedValue(initialDeviceId);
        }
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
                        label:
                            device.label ||
                            (kind === "videoinput"
                                ? `Камера ${index + 1}`
                                : `Микрофон ${index + 1}`),
                        value: device.deviceId,
                    }));
                setOptions(filteredDevices);
                // Pre-select stored device if available, else first device
                const hasStored = initialDeviceId && filteredDevices.some((d) => d.value === initialDeviceId);
                if (!hasStored && filteredDevices.length > 0) {
                    setSelectedValue(filteredDevices[0].value);
                }
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
