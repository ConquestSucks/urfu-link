import type { NotificationBadgeDto } from "@urfu-link/api-client";
import { create } from "zustand";

type NotificationConnectionState = {
    isConnected: boolean;
    badge: NotificationBadgeDto | null;
    setConnected: (isConnected: boolean) => void;
    setBadge: (badge: NotificationBadgeDto | null) => void;
};

export const useNotificationStore = create<NotificationConnectionState>((set) => ({
    isConnected: false,
    badge: null,
    setConnected: (isConnected) => set({ isConnected }),
    setBadge: (badge) => set({ badge }),
}));
