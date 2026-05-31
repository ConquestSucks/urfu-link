import { appConfig } from "./config";
import { useAuthStore } from "@/shared/store/auth-store";
import { Platform } from "react-native";

type SignalRModule = typeof import("@microsoft/signalr");

export type HubConnection = import("@microsoft/signalr").HubConnection;

export const HUB_CONNECTION_STATE = {
    Connected: "Connected",
    Connecting: "Connecting",
    Disconnected: "Disconnected",
    Disconnecting: "Disconnecting",
    Reconnecting: "Reconnecting",
} as const;

let signalRModulePromise: Promise<SignalRModule> | null = null;

const getSignalRModule = () => {
    signalRModulePromise ??= import("@microsoft/signalr");
    return signalRModulePromise;
};

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

export const createHubConnection = async (hubPath: string): Promise<HubConnection> => {
    const signalR = await getSignalRModule();
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
