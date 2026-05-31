export const APP_ENVS = ["dev", "prod"] as const;
export type AppEnv = (typeof APP_ENVS)[number];
export type RuntimeConfigInput = {
    appEnv: AppEnv;
    apiUrl: string;
    keycloakUrl: string;
};

type RuntimeConfigResult =
    | { success: true; data: RuntimeConfigInput }
    | { success: false; error: { message: string } };

const isAppEnv = (value: unknown): value is AppEnv =>
    typeof value === "string" && (APP_ENVS as readonly string[]).includes(value);

const isUrl = (value: unknown): value is string => {
    if (typeof value !== "string") return false;

    try {
        new URL(value);
        return true;
    } catch {
        return false;
    }
};

export const runtimeConfigSchema = {
    safeParse(value: Partial<RuntimeConfigInput>): RuntimeConfigResult {
        const issues: string[] = [];
        const appEnv = isAppEnv(value.appEnv) ? value.appEnv : null;
        const apiUrl = isUrl(value.apiUrl) ? value.apiUrl : null;
        const keycloakUrl = isUrl(value.keycloakUrl) ? value.keycloakUrl : null;

        if (!appEnv) {
            issues.push("appEnv must be one of: dev, prod");
        }

        if (!apiUrl) {
            issues.push("apiUrl must be a valid URL");
        }

        if (!keycloakUrl) {
            issues.push("keycloakUrl must be a valid URL");
        }

        if (issues.length > 0) {
            return { success: false, error: { message: issues.join("; ") } };
        }

        return {
            success: true,
            data: {
                appEnv: appEnv!,
                apiUrl: apiUrl!,
                keycloakUrl: keycloakUrl!,
            },
        };
    },
};
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
