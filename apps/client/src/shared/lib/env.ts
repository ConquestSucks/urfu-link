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
// Defaults exist only for the local dev loop. prod must come from EXPO_PUBLIC_*
// (eas.json sets them per profile, docker-entrypoint.sh sets them at container start).
export const DEFAULT_API_URLS: Partial<Record<AppEnv, string>> = {
    dev: "http://localhost:5080",
};
export const DEFAULT_KEYCLOAK_URLS: Partial<Record<AppEnv, string>> = {
    dev: "http://localhost:8080",
};
export const ENV_KEYS = {
    APP_ENV: "APP_ENV",
    EXPO_PUBLIC_API_URL: "EXPO_PUBLIC_API_URL",
    EXPO_PUBLIC_KEYCLOAK_URL: "EXPO_PUBLIC_KEYCLOAK_URL",
    EXPO_EAS_PROJECT_ID: "EXPO_EAS_PROJECT_ID",
    EXPO_OWNER: "EXPO_OWNER",
} as const;
