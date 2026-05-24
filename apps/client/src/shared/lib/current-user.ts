import { apiClient } from "./api";
import { useAuthStore } from "@/shared/store/auth-store";

let currentUserIdPromise: Promise<string> | null = null;

type ResolveCurrentUserIdOptions = {
    forceRefresh?: boolean;
};

const loadCurrentUserId = () =>
    apiClient.users.getMe()
        .then((profile) => {
            useAuthStore.getState().setUserId(profile.userId);
            return profile.userId;
        })
        .finally(() => {
            currentUserIdPromise = null;
        });

export const resolveCurrentUserId = async ({
    forceRefresh = false,
}: ResolveCurrentUserIdOptions = {}): Promise<string> => {
    if (currentUserIdPromise) {
        return currentUserIdPromise;
    }

    const { userId } = useAuthStore.getState();
    if (!forceRefresh && userId) {
        return userId;
    }

    currentUserIdPromise = loadCurrentUserId();
    return currentUserIdPromise;
};
