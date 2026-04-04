import { createApiClient } from "@urfu-link/api-client";
import { Platform } from "react-native";
import { appConfig } from "./config";
import { useAuthStore } from "@/shared/store/auth-store";

// Dev web: cross-origin requests to gateway at configured apiUrl
// Prod web: same-origin through Pomerium (empty baseUrl)
const baseUrl =
    Platform.OS !== "web" || appConfig.appEnv === "dev"
        ? appConfig.apiUrl
        : "";

export const apiClient = createApiClient({
    baseUrl,
    getAccessToken: () => useAuthStore.getState().accessToken ?? undefined,
    onUnauthorized:
        appConfig.appEnv === "dev"
            ? () => useAuthStore.getState().clearTokens()
            : undefined,
});
