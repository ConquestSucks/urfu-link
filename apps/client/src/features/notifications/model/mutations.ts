import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { NotificationListStatus } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";
import { notificationKeys } from "./queries";

const useInvalidateNotifications = () => {
    const queryClient = useQueryClient();
    return () => {
        queryClient.invalidateQueries({ queryKey: notificationKeys.all });
    };
};

export function useMarkNotificationRead() {
    const invalidate = useInvalidateNotifications();
    return useMutation({
        mutationFn: (id: string) => apiClient.notifications.markRead(id),
        onSuccess: invalidate,
    });
}

export function useMarkNotificationDone() {
    const invalidate = useInvalidateNotifications();
    return useMutation({
        mutationFn: (id: string) => apiClient.notifications.markDone(id),
        onSuccess: invalidate,
    });
}

export function useMarkAllNotificationsRead(status: NotificationListStatus = "unread") {
    const invalidate = useInvalidateNotifications();
    return useMutation({
        mutationFn: () =>
            apiClient.notifications.bulk({
                action: "read",
                filter: { status },
            }),
        onSuccess: invalidate,
    });
}
