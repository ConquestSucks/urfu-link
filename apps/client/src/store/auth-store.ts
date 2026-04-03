import { appStorage } from "@/lib/storage";
import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";

type AuthState = {
    accessToken: string | null;
    refreshToken: string | null;
    expiresAt: number | null; // Unix ms
    setTokens: (accessToken: string, refreshToken: string, expiresAt: number) => void;
    clearTokens: () => void;
    isTokenValid: () => boolean;
};

export const useAuthStore = create<AuthState>()(
    persist(
        (set, get) => ({
            accessToken: null,
            refreshToken: null,
            expiresAt: null,
            setTokens: (accessToken, refreshToken, expiresAt) =>
                set({ accessToken, refreshToken, expiresAt }),
            clearTokens: () =>
                set({ accessToken: null, refreshToken: null, expiresAt: null }),
            isTokenValid: () => {
                const { accessToken, expiresAt } = get();
                if (!accessToken || expiresAt === null) return false;
                // Consider valid if more than 30 seconds remaining
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
            }),
        }
    )
);
