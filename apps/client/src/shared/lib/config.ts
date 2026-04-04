import Constants from "expo-constants";
import { Platform } from "react-native";
import type { AppEnv } from "./env";
import { DEFAULT_API_URLS, DEFAULT_KEYCLOAK_URL, runtimeConfigSchema } from "./env";
export type { AppEnv } from "./env";

export type RuntimeConfig = {
    appEnv: AppEnv;
    apiUrl: string;
    keycloakUrl: string;
};
declare global {
    interface Window {
        __APP_CONFIG__?: Partial<RuntimeConfig>;
    }
}
const defaultConfig: RuntimeConfig = {
    appEnv: "dev",
    apiUrl: DEFAULT_API_URLS.dev,
    keycloakUrl: DEFAULT_KEYCLOAK_URL,
};
function getRawConfig(): Partial<RuntimeConfig> {
    const extra = (Constants.expoConfig?.extra ?? {}) as Partial<RuntimeConfig>;
    const webConfig = Platform.OS === "web" && typeof window !== "undefined" ? window.__APP_CONFIG__ ?? {} : {};
    return {
        appEnv: webConfig.appEnv ?? extra.appEnv ?? defaultConfig.appEnv,
        apiUrl: webConfig.apiUrl ?? extra.apiUrl ?? defaultConfig.apiUrl,
        keycloakUrl: webConfig.keycloakUrl ?? extra.keycloakUrl ?? defaultConfig.keycloakUrl,
    };
}
const raw = getRawConfig();
const parsed = runtimeConfigSchema.safeParse(raw);
if (!parsed.success) {
    throw new Error(`Invalid app config: ${parsed.error.message}. Raw: ${JSON.stringify(raw)}`, { cause: parsed.error });
}
export const appConfig: RuntimeConfig = parsed.data;
