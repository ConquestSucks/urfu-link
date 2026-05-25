import { useEffect } from "react";
import { AppState, AppStateStatus } from "react-native";
import { HubConnectionState } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import type { NotificationBadgeDto, NotificationDto } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { useNotificationStore } from "@/shared/store/notification-store";
import { notificationKeys } from "./queries";

export function useNotificationHub() {
    const queryClient = useQueryClient();
    const setConnected = useNotificationStore((s) => s.setConnected);
    const setBadge = useNotificationStore((s) => s.setBadge);

    useEffect(() => {
        const connection = createHubConnection("/hubs/notifications");

        const invalidateNotifications = () => {
            queryClient.invalidateQueries({ queryKey: notificationKeys.all });
        };

        connection.on("BadgeUpdated", (badge: NotificationBadgeDto) => {
            setBadge(badge);
            queryClient.setQueryData(notificationKeys.badge(), badge);
        });

        connection.on("NotificationReceived", (_notification: NotificationDto) => {
            invalidateNotifications();
        });

        connection.on("NotificationUpserted", (_notification: NotificationDto) => {
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
            if (connection.state === HubConnectionState.Connected) {
                return;
            }

            try {
                await connection.start();
                setConnected(true);
            } catch (error) {
                console.warn("NotificationHub connection failed", error);
                setConnected(false);
            }
        };

        void connect();

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                void connect();
            }
        });

        return () => {
            subscription.remove();
            void connection.stop();
            setConnected(false);
        };
    }, [queryClient, setBadge, setConnected]);
}
