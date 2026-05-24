import Constants from "expo-constants";
import { Platform } from "react-native";
import type { AppEnv } from "./env";
import { runtimeConfigSchema } from "./env";
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
function getRawConfig(): Partial<RuntimeConfig> {
    const extra = (Constants.expoConfig?.extra ?? {}) as Partial<RuntimeConfig>;
    const webConfig = Platform.OS === "web" && typeof window !== "undefined" ? window.__APP_CONFIG__ ?? {} : {};
    // extra is populated at build time by app.config.ts (which already validates required env).
    // webConfig wins in prod where docker-entrypoint.sh writes /app-config.js from container env.
    return {
        appEnv: webConfig.appEnv || extra.appEnv,
        apiUrl: webConfig.apiUrl || extra.apiUrl,
        keycloakUrl: webConfig.keycloakUrl || extra.keycloakUrl,
    };
}
const raw = getRawConfig();
const parsed = runtimeConfigSchema.safeParse(raw);
if (!parsed.success) {
    throw new Error(`Invalid app config: ${parsed.error.message}. Raw: ${JSON.stringify(raw)}`, { cause: parsed.error });
}
export const appConfig: RuntimeConfig = parsed.data;
