import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import type { ListNotificationsParams, NotificationListStatus } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";

export const notificationKeys = {
    all: ["notifications"] as const,
    lists: () => [...notificationKeys.all, "list"] as const,
    list: (status: NotificationListStatus, query: string) =>
        [...notificationKeys.lists(), { status, query }] as const,
    badge: () => [...notificationKeys.all, "badge"] as const,
};

export function useNotificationBadge() {
    return useQuery({
        queryKey: notificationKeys.badge(),
        queryFn: () => apiClient.notifications.getBadge(),
        staleTime: 15_000,
    });
}

export function useNotifications(status: NotificationListStatus, query = "") {
    return useInfiniteQuery({
        queryKey: notificationKeys.list(status, query),
        initialPageParam: undefined as string | undefined,
        queryFn: ({ pageParam }) => {
            const params: ListNotificationsParams = {
                cursor: pageParam,
                limit: 30,
                status,
            };
            if (query.trim().length > 0) {
                params.query = query.trim();
            }

            return apiClient.notifications.list(params);
        },
        getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    });
}
