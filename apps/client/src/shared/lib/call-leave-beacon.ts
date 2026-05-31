import { Platform } from "react-native";
import { appConfig } from "./config";
import { useAuthStore } from "@/shared/store/auth-store";

const getBaseUrl = () =>
    Platform.OS !== "web" || appConfig.appEnv === "dev"
        ? appConfig.apiUrl
        : "";

export const sendCallLeaveBeacon = (callId: string) => {
    if (Platform.OS !== "web") return;

    const accessToken = useAuthStore.getState().accessToken;
    if (!accessToken) return;

    const url = `${getBaseUrl()}/api/calls/${encodeURIComponent(callId)}/leave`;
    try {
        void fetch(url, {
            method: "POST",
            headers: {
                Authorization: `Bearer ${accessToken}`,
            },
            keepalive: true,
        });
    } catch {
        // The page may already be unloading; the call TTL remains the fallback.
    }
};
