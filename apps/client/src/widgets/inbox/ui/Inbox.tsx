import React from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { InboxChatSkeleton } from "@/entities/inbox-chat";
import { InboxNotificationSkeleton } from "@/entities/inbox-notification";
import { InboxSubjectSkeleton } from "@/entities/inbox-subject";
import { EmptyState } from "@/shared/ui";
import { Header } from "./Header";
import { List } from "./List";
import { NotificationListToolbar } from "./NotificationListToolbar";
import { InboxListProps } from "../model/components";
import { getInboxEmptyState } from "../lib/empty-state-config";
import { GlobalSearchPanel, useSearchStore } from "@/features/chat-search";

interface InboxProps<T> extends InboxListProps<T> {
    data: T[];
    renderItem: (item: T) => React.ReactNode;
    isLoading?: boolean;
    notificationUnreadCount?: number;
    isMarkingAllNotificationsRead?: boolean;
    onMarkAllNotificationsRead?: () => void;
}

export const Inbox = <T,>({
    data,
    renderItem,
    isLoading,
    notificationUnreadCount = 0,
    isMarkingAllNotificationsRead,
    onMarkAllNotificationsRead,
}: InboxProps<T>) => {
    const { currentTab, currentView } = useInboxRouting();
    const globalQuery = useSearchStore((s) => s.globalQuery);
    const isSearchActive = globalQuery.length >= 2;

    const title =
        currentView === "notifications"
            ? "Уведомления"
            : currentTab === "chats"
              ? "Личные чаты"
              : "Предметы";
    const showNotificationToolbar =
        currentView === "notifications" && !isSearchActive && data.length > 0;

    return (
        <View className="bg-app-panel w-[calc(384/1359*100vw)] h-full gap-4 border-r border-white/5">
            <Header title={title} />

            {showNotificationToolbar && (
                <NotificationListToolbar
                    unreadCount={notificationUnreadCount}
                    isMarkingAllRead={isMarkingAllNotificationsRead}
                    onMarkAllRead={onMarkAllNotificationsRead}
                />
            )}

            {isSearchActive ? (
                <GlobalSearchPanel />
            ) : isLoading && data.length === 0 ? (
                <View className="px-3 gap-2 overflow-hidden">
                    {[...Array(currentTab === "subjects" ? 3 : 7)].map((_, index) =>
                        currentView === "notifications" ? (
                            <InboxNotificationSkeleton key={index} />
                        ) : currentTab === "subjects" ? (
                            <InboxSubjectSkeleton key={index} />
                        ) : (
                            <InboxChatSkeleton key={index} />
                        ),
                    )}
                </View>
            ) : data.length === 0 ? (
                <EmptyState size="full" {...getInboxEmptyState(currentTab, currentView)} />
            ) : (
                <List key={`${currentTab}-${currentView}`} data={data} renderItem={renderItem} />
            )}
        </View>
    );
};
