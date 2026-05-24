import * as signalR from "@microsoft/signalr";
import { appConfig } from "./config";
import { useAuthStore } from "@/shared/store/auth-store";
import { Platform } from "react-native";

const createPresenceDeviceId = () => {
    const randomUuid = globalThis.crypto?.randomUUID?.();
    if (randomUuid) return randomUuid;

    return `presence-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
};

const presenceDeviceId = createPresenceDeviceId();

const getBaseUrl = () =>
    Platform.OS !== "web" || appConfig.appEnv === "dev"
        ? appConfig.apiUrl
        : "";

const appendQuery = (url: string, params: Record<string, string>) => {
    const query = new URLSearchParams(params).toString();
    return `${url}${url.includes("?") ? "&" : "?"}${query}`;
};

export const createHubConnection = (hubPath: string) => {
    const baseUrl = getBaseUrl();

    const url = hubPath === "/hubs/presence"
        ? appendQuery(`${baseUrl}${hubPath}`, {
            deviceId: presenceDeviceId,
            platform: Platform.OS === "web" ? "Web" : "Mobile",
        })
        : `${baseUrl}${hubPath}`;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url, {
            accessTokenFactory: () => useAuthStore.getState().accessToken ?? "",
        })
        .withAutomaticReconnect()
        .build();

    return connection;
};

export const notifyPresenceDisconnect = () => {
    const accessToken = useAuthStore.getState().accessToken;
    if (!accessToken) return;

    const url = `${getBaseUrl()}/api/presence/sessions/${encodeURIComponent(presenceDeviceId)}/disconnect`;
    try {
        void fetch(url, {
            method: "POST",
            headers: {
                Authorization: `Bearer ${accessToken}`,
            },
            keepalive: true,
        });
    } catch {
        // The page may already be unloading; the Redis TTL sweeper remains the fallback.
    }
};
