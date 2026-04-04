import { appConfig } from "@/shared/lib/config";
import { useAuthStore } from "@/shared/store/auth-store";
import { useCallback, useEffect, useRef } from "react";
import type { PropsWithChildren } from "react";

async function refreshAccessToken(
    keycloakUrl: string,
    refreshToken: string,
): Promise<{ accessToken: string; refreshToken: string; expiresAt: number } | null> {
    try {
        const res = await fetch(`${keycloakUrl}/realms/urfu-link/protocol/openid-connect/token`, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: new URLSearchParams({
                grant_type: "refresh_token",
                client_id: "urfu-link-web",
                refresh_token: refreshToken,
            }).toString(),
        });
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

export default function AuthProvider({ children }: PropsWithChildren) {
    const { accessToken, setTokens, clearTokens } = useAuthStore();
    const refreshTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const isDev = appConfig.appEnv === "dev";

    const scheduleRefresh = useCallback(
        (expiresAtMs: number) => {
            if (!isDev) return;
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
        },
        [isDev, setTokens, clearTokens],
    );

    // Dev-only: initial refresh attempt on mount; prod uses Pomerium — no token management.
    useEffect(() => {
        if (!isDev) return;

        const tryRefresh = async () => {
            const { isTokenValid: valid, refreshToken: rt, expiresAt: exp, setTokens: set, clearTokens: clear } =
                useAuthStore.getState();
            if (valid()) {
                if (exp) scheduleRefresh(exp);
                return;
            }
            if (rt) {
                const result = await refreshAccessToken(appConfig.keycloakUrl, rt);
                if (result) {
                    set(result.accessToken, result.refreshToken, result.expiresAt);
                    scheduleRefresh(result.expiresAt);
                } else {
                    clear();
                }
            }
        };
        void tryRefresh();

        return () => {
            if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current);
        };
    }, [isDev, scheduleRefresh]);

    // Dev-only: schedule refresh when AuthGate sets a new token.
    useEffect(() => {
        if (!isDev) return;
        const { expiresAt: exp, isTokenValid: valid } = useAuthStore.getState();
        if (accessToken && exp && valid()) {
            scheduleRefresh(exp);
        }
    }, [isDev, accessToken, scheduleRefresh]);

    return <>{children}</>;
}
