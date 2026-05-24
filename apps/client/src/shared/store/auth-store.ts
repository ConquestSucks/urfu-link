import { appStorage } from "../lib/storage";
import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";

type AuthState = {
    accessToken: string | null;
    refreshToken: string | null;
    expiresAt: number | null; // Unix ms
    userId: string | null;
    setTokens: (accessToken: string, refreshToken: string, expiresAt: number) => void;
    setUserId: (userId: string | null) => void;
    clearTokens: () => void;
    isTokenValid: () => boolean;
};

/**
 * Извлекает sub claim (UUID пользователя) из JWT access token.
 * atob/btoa уже используются в проекте (см. auth-gate.tsx), доступны в Hermes 2024+.
 */
const extractUserId = (token: string): string | null => {
    try {
        const parts = token.split(".");
        if (parts.length !== 3) return null;
        const payload = parts[1];
        // base64url → base64 + padding
        const b64 = payload.replace(/-/g, "+").replace(/_/g, "/");
        const padded = b64 + "=".repeat((4 - (b64.length % 4)) % 4);
        const decoded = JSON.parse(atob(padded));
        return typeof decoded.sub === "string" ? decoded.sub : null;
    } catch {
        return null;
    }
};

export const useAuthStore = create<AuthState>()(
    persist(
        (set, get) => ({
            accessToken: null,
            refreshToken: null,
            expiresAt: null,
            userId: null,
            setTokens: (accessToken, refreshToken, expiresAt) =>
                set({
                    accessToken,
                    refreshToken,
                    expiresAt,
                    userId: extractUserId(accessToken),
                }),
            setUserId: (userId) => set({ userId }),
            clearTokens: () =>
                set({
                    accessToken: null,
                    refreshToken: null,
                    expiresAt: null,
                    userId: null,
                }),
            isTokenValid: () => {
                const { accessToken, expiresAt } = get();
                if (!accessToken || expiresAt === null) return false;
                // Considered valid if more than 30 seconds remaining
                return expiresAt > Date.now() + 30_000;
            },
        }),
        {
            name: "urfu-link-auth",
            storage: createJSONStorage(() => appStorage),
            partialize: (state) => ({
                accessToken: state.accessToken,
                refreshToken: state.refreshToken,
                expiresAt: state.expiresAt,
                userId: state.userId,
            }),
        }
    )
);

/** Текущий userId из JWT, либо null если не залогинен / token малформен. */
export const useCurrentUserId = () => useAuthStore((s) => s.userId);
