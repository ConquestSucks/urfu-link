import { useCallback, useState } from "react";
import { Platform } from "react-native";

type DeviceKind = "audioinput" | "videoinput" | "audiooutput";
type PermissionStatus =
  | "idle"
  | "granted"
  | "denied"
  | "not_found"
  | "busy"
  | "error";

export const useMediaDevices = (kind: DeviceKind) => {
  const [options, setOptions] = useState<{ label: string; value: string }[]>(
    [],
  );
  const [selectedValue, setSelectedValue] = useState<string | number>("");
  const [isLoading, setIsLoading] = useState(false);
  const [permissionStatus, setPermissionStatus] =
    useState<PermissionStatus>("idle");

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
        if (filteredDevices.length > 0) {
          setSelectedValue(filteredDevices[0].value);
        }
        setPermissionStatus("granted");

        stream.getTracks().forEach((track) => track.stop());
      } else {
        setTimeout(() => {
          setOptions([{ label: "Системное устройство", value: "default" }]);
          setSelectedValue("default");
          setPermissionStatus("granted");
          setIsLoading(false);
        }, 800);
      }
    } catch (error: any) {
      console.warn(`[WebRTC Error] ${kind}:`, error.name, error.message);

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
  }, [kind, permissionStatus]);

  return {
    options,
    selectedValue,
    setSelectedValue,
    isLoading,
    permissionStatus,
    requestAccess,
  };
};
