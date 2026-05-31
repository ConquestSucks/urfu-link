import { useEffect } from "react";
import { AppState, AppStateStatus } from "react-native";
import { HubConnectionState } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import type { NotificationBadgeDto, NotificationDto } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { useNotificationStore } from "@/shared/store/notification-store";
import { showServerBrowserNotification } from "@/shared/lib/browser-notifications";
import { notificationKeys } from "./queries";

export function useNotificationHub() {
    const queryClient = useQueryClient();
    const setConnected = useNotificationStore((s) => s.setConnected);
    const setBadge = useNotificationStore((s) => s.setBadge);

    useEffect(() => {
        const connection = createHubConnection("/hubs/notifications");
        let retryTimer: ReturnType<typeof setTimeout> | null = null;
        let retryAttempt = 0;
        let disposed = false;
        let isPageVisible = true;

        const invalidateNotifications = () => {
            queryClient.invalidateQueries({ queryKey: notificationKeys.all });
        };

        const clearRetry = () => {
            if (!retryTimer) return;
            clearTimeout(retryTimer);
            retryTimer = null;
        };

        const scheduleRetry = () => {
            if (disposed || !isPageVisible || retryTimer) return;
            const delay = Math.min(1_000 * 2 ** retryAttempt, 10_000);
            retryAttempt += 1;
            retryTimer = setTimeout(() => {
                retryTimer = null;
                void connect();
            }, delay);
        };

        connection.on("BadgeUpdated", (badge: NotificationBadgeDto) => {
            setBadge(badge);
            queryClient.setQueryData(notificationKeys.badge(), badge);
        });

        connection.on("NotificationReceived", (notification: NotificationDto) => {
            showServerBrowserNotification(notification);
            invalidateNotifications();
        });

        connection.on("NotificationUpserted", (notification: NotificationDto) => {
            showServerBrowserNotification(notification);
            invalidateNotifications();
        });

        connection.on("NotificationStateChanged", () => {
            invalidateNotifications();
        });

        connection.on("NotificationRemoved", () => {
            invalidateNotifications();
        });

        connection.on("NotificationBackfillRequired", () => {
            invalidateNotifications();
        });

        connection.onreconnected(() => {
            retryAttempt = 0;
            clearRetry();
            setConnected(true);
            invalidateNotifications();
        });

        connection.onreconnecting(() => {
            setConnected(false);
        });

        connection.onclose(() => {
            setConnected(false);
        });

        const connect = async () => {
            if (
                disposed ||
                !isPageVisible ||
                connection.state === HubConnectionState.Connected ||
                connection.state === HubConnectionState.Connecting
            ) {
                return;
            }

            try {
                clearRetry();
                await connection.start();
                retryAttempt = 0;
                setConnected(true);
            } catch (error) {
                console.warn("NotificationHub connection failed", error);
                setConnected(false);
                scheduleRetry();
            }
        };

        void connect();

        const handlePageHide = () => {
            isPageVisible = false;
            clearRetry();
            void connection.stop();
            setConnected(false);
        };

        const handlePageShow = () => {
            isPageVisible = true;
            void connect();
        };

        if (typeof window !== "undefined") {
            window.addEventListener("pagehide", handlePageHide);
            window.addEventListener("pageshow", handlePageShow);
        }

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                void connect();
            }
        });

        return () => {
            disposed = true;
            clearRetry();
            if (typeof window !== "undefined") {
                window.removeEventListener("pagehide", handlePageHide);
                window.removeEventListener("pageshow", handlePageShow);
            }
            subscription.remove();
            void connection.stop();
            setConnected(false);
        };
    }, [queryClient, setBadge, setConnected]);
}
