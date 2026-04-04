import { appConfig } from "@/lib/config";
import { useAuthStore } from "@/store/auth-store";
import { useEffect, useRef } from "react";
import type { PropsWithChildren } from "react";
import { KEYCLOAK_CLIENT_ID, KEYCLOAK_REALM } from "@/features/auth/keycloak-constants";

async function refreshAccessToken(
    keycloakUrl: string,
    refreshToken: string
): Promise<{ accessToken: string; refreshToken: string; expiresAt: number } | null> {
    try {
        const res = await fetch(
            `${keycloakUrl}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`,
            {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: new URLSearchParams({
                    grant_type: "refresh_token",
                    client_id: KEYCLOAK_CLIENT_ID,
                    refresh_token: refreshToken,
                }).toString(),
            }
        );
        if (!res.ok) return null;
        const data = await res.json();
        return {
            accessToken: data.access_token,
            refreshToken: data.refresh_token ?? refreshToken,
            expiresAt: Date.now() + data.expires_in * 1000,
        };
    } catch {
        return null;
    }
}

export function AuthProvider({ children }: PropsWithChildren) {
    const { accessToken, refreshToken, expiresAt, setTokens, clearTokens, isTokenValid } =
        useAuthStore();
    const refreshTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    // In production Pomerium handles auth — no token management needed
    if (appConfig.appEnv !== "dev") {
        return <>{children}</>;
    }

    const scheduleRefresh = (expiresAtMs: number) => {
        if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current);
        const delay = Math.max(expiresAtMs - Date.now() - 60_000, 0);
        refreshTimerRef.current = setTimeout(async () => {
            const currentRefreshToken = useAuthStore.getState().refreshToken;
            if (!currentRefreshToken) return;
            const result = await refreshAccessToken(appConfig.keycloakUrl, currentRefreshToken);
            if (result) {
                setTokens(result.accessToken, result.refreshToken, result.expiresAt);
                scheduleRefresh(result.expiresAt);
            } else {
                clearTokens();
            }
        }, delay);
    };

    useEffect(() => {
        const tryRefresh = async () => {
            if (isTokenValid()) {
                if (expiresAt) scheduleRefresh(expiresAt);
                return;
            }
            if (refreshToken) {
                const result = await refreshAccessToken(appConfig.keycloakUrl, refreshToken);
                if (result) {
                    setTokens(result.accessToken, result.refreshToken, result.expiresAt);
                    scheduleRefresh(result.expiresAt);
                } else {
                    clearTokens();
                }
            }
        };
        tryRefresh();
        return () => {
            if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current);
        };
    }, []); // eslint-disable-line react-hooks/exhaustive-deps

    // Schedule refresh when a new token is set from AuthGate
    useEffect(() => {
        if (accessToken && expiresAt && isTokenValid()) {
            scheduleRefresh(expiresAt);
        }
    }, [accessToken]); // eslint-disable-line react-hooks/exhaustive-deps

    return <>{children}</>;
}
