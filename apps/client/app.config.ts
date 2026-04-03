import type { ConfigContext, ExpoConfig } from "expo/config";
import { z } from "zod";
const APP_ENVS = ["dev", "prod"] as const;
const appEnvSchema = z.enum(APP_ENVS);
const DEFAULT_API_URLS: Record<(typeof APP_ENVS)[number], string> = {
    dev: "https://api.dev.127.0.0.1.nip.io",
    prod: "https://api.ghjc.ru",
};
const DEFAULT_KEYCLOAK_URL = "http://localhost:8080";
const runtimeConfigSchema = z.object({
    appEnv: appEnvSchema,
    apiUrl: z.string().url(),
    keycloakUrl: z.string().url(),
});
type AppExpoConfig = ExpoConfig & {
    newArchEnabled?: boolean;
};
export default ({ config }: ConfigContext): AppExpoConfig => {
    const appEnvResult = appEnvSchema.safeParse(process.env.APP_ENV);
    const appEnv = appEnvResult.success ? appEnvResult.data : "dev";
    const apiUrl = process.env.EXPO_PUBLIC_API_URL ?? DEFAULT_API_URLS[appEnv];
    const keycloakUrl = process.env.EXPO_PUBLIC_KEYCLOAK_URL ?? DEFAULT_KEYCLOAK_URL;
    const buildConfig = { appEnv, apiUrl, keycloakUrl };
    const validated = runtimeConfigSchema.safeParse(buildConfig);
    if (!validated.success) {
        throw new Error(`Invalid build-time config: ${validated.error.message}`, {
            cause: validated.error,
        });
    }
    const projectId = process.env.EXPO_EAS_PROJECT_ID;
    return {
        ...config,
        name: appEnv === "prod" ? "URFU Link" : "URFU Link Dev",
        slug: "urfu-link-client",
        version: "1.0.0",
        scheme: "urfulink",
        orientation: "portrait",
        userInterfaceStyle: "automatic",
        jsEngine: "hermes",
        newArchEnabled: true,
        platforms: ["ios", "android", "web"],
        runtimeVersion: {
            policy: "appVersion"
        },
        experiments: {
            typedRoutes: true
        },
        plugins: ["expo-router", "expo-web-browser", "expo-image-picker"],
        extra: {
            appEnv,
            apiUrl,
            keycloakUrl,
            ...(projectId
                ? {
                    eas: {
                        projectId
                    }
                }
                : {})
        },
        ...(process.env.EXPO_OWNER
            ? {
                owner: process.env.EXPO_OWNER
            }
            : {}),
        ...(projectId
            ? {
                updates: {
                    url: `https://u.expo.dev/${projectId}`
                }
            }
            : {}),
        web: {
            bundler: "metro"
        }
    };
};
