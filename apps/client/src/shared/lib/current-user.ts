import { apiClient } from "./api";
import { useAuthStore } from "@/shared/store/auth-store";
import type { UserProfile } from "@urfu-link/api-client";

let currentUserPromise: Promise<UserProfile> | null = null;
let currentUserCache: UserProfile | null = null;

type ResolveCurrentUserIdOptions = {
    forceRefresh?: boolean;
};

const loadCurrentUser = () =>
    apiClient.users.getMe()
        .then((profile) => {
            currentUserCache = profile;
            useAuthStore.getState().setUserId(profile.userId);
            return profile;
        })
        .finally(() => {
            currentUserPromise = null;
        });

export const resolveCurrentUser = async ({
    forceRefresh = false,
}: ResolveCurrentUserIdOptions = {}): Promise<UserProfile> => {
    if (currentUserPromise) {
        return currentUserPromise;
    }

    if (!forceRefresh && currentUserCache) {
        return currentUserCache;
    }

    currentUserPromise = loadCurrentUser();
    return currentUserPromise;
};

export const resolveCurrentUserId = async (
    { forceRefresh = false }: ResolveCurrentUserIdOptions = {},
): Promise<string> => {
    if (!forceRefresh) {
        const { userId } = useAuthStore.getState();
        if (userId) {
            return userId;
        }
    }

    const profile = await resolveCurrentUser({ forceRefresh });
    return profile.userId;
};
