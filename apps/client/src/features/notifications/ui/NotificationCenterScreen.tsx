import type { NotificationListStatus } from "@urfu-link/api-client";
import React, { useMemo, useState } from "react";
import { NotificationCenter } from "@/widgets/notifications";
import {
    useMarkNotificationDone,
    useMarkNotificationRead,
    useNotifications,
} from "@/features/notifications";

export const NotificationCenterScreen = () => {
    const [filter, setFilter] = useState<NotificationListStatus>("all");
    const notifications = useNotifications(filter);
    const markRead = useMarkNotificationRead();
    const markDone = useMarkNotificationDone();

    const items = useMemo(
        () => notifications.data?.pages.flatMap((page) => page.items) ?? [],
        [notifications.data],
    );

    return (
        <NotificationCenter
            items={items}
            isLoading={notifications.isLoading}
            filter={filter}
            onFilterChange={setFilter}
            onMarkRead={(id) => markRead.mutate(id)}
            onMarkDone={(id) => markDone.mutate(id)}
            onLoadMore={() => notifications.fetchNextPage()}
            hasMore={notifications.hasNextPage ?? false}
        />
    );
};
