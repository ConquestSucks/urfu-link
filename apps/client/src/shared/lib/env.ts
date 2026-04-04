import { z } from "zod";
export const APP_ENVS = ["dev", "prod"] as const;
export type AppEnv = (typeof APP_ENVS)[number];
export const appEnvSchema = z.enum(APP_ENVS);
export const runtimeConfigSchema = z.object({
    appEnv: appEnvSchema,
    apiUrl: z.string().url(),
    keycloakUrl: z.string().url(),
});
export type RuntimeConfigInput = z.infer<typeof runtimeConfigSchema>;
export const DEFAULT_API_URLS: Record<AppEnv, string> = {
    dev: "https://api.dev.127.0.0.1.nip.io",
    prod: "https://api.urfu-link.ghjc.ru",
};
export const DEFAULT_KEYCLOAK_URL = "http://localhost:8080";
export const ENV_KEYS = {
    APP_ENV: "APP_ENV",
    EXPO_PUBLIC_API_URL: "EXPO_PUBLIC_API_URL",
    EXPO_PUBLIC_KEYCLOAK_URL: "EXPO_PUBLIC_KEYCLOAK_URL",
    EXPO_EAS_PROJECT_ID: "EXPO_EAS_PROJECT_ID",
    EXPO_OWNER: "EXPO_OWNER",
} as const;
