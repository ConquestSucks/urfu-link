import { useCallback, useEffect, useMemo } from "react";
import { View } from "react-native";
import { router, useSegments } from "expo-router";

import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification, InboxNotificationProps } from "@/entities/inbox-notification";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { MasterDetailLayout } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useInboxStore } from "@/shared/store/useInboxStore";
import { resolveNotificationDeepLink } from "@/shared/lib/notificationDeepLink";
import { useInboxConversations } from "../model/use-inbox-conversations";
import { Inbox } from "./Inbox";
import { InboxMobile } from "./InboxMobile";

export const InboxLayout = () => {
    const segments = useSegments() as string[];
    const { isMobile } = useWindowSize();

    const { currentTab, currentView, params } = useInboxRouting();

    const conversationsLoading = useChatStore((s) => s.isConversationsLoading);
    const loadConversations = useChatStore((s) => s.loadConversations);
    const setPendingScrollToMessageId = useChatStore(
        (s) => s.setPendingScrollToMessageId,
    );
    const chats = useInboxConversations("chats");
    const subjects = useInboxConversations("subjects");

    const notifications = useInboxStore((s) => s.notifications);
    const isNotificationsLoading = useInboxStore((s) => s.isNotificationsLoading);
    const isMarkingAllNotificationsRead = useInboxStore(
        (s) => s.isMarkingAllNotificationsRead,
    );
    const fetchNotifications = useInboxStore((s) => s.fetchNotifications);
    const markNotificationRead = useInboxStore((s) => s.markNotificationRead);
    const markAllNotificationsRead = useInboxStore((s) => s.markAllNotificationsRead);

    const isDetailView = segments.includes("[id]");

    // params.view не нормализуем через router.setParams: expo-router падает на
    // первом рендере с "Attempted to navigate before mounting the Root Layout"
    // в момент HMR/cold-mount. useInboxRouting уже отдаёт "messages" по умолчанию,
    // когда query-параметр отсутствует — этого достаточно.

    useEffect(() => {
        if (currentView === "notifications") {
            fetchNotifications();
            return;
        }
        loadConversations(currentTab === "chats" ? "Direct" : "Discipline");
    }, [currentView, currentTab, fetchNotifications, loadConversations]);

    const currentData = useMemo(() => {
        if (currentView === "notifications") {
            return notifications.filter((n) => n.scope === currentTab);
        }
        return currentTab === "chats" ? chats : subjects;
    }, [currentView, currentTab, chats, subjects, notifications]);

    const isLoading =
        currentView === "notifications" ? isNotificationsLoading : conversationsLoading;

    const unreadNotificationIds = useMemo(() => {
        if (currentView !== "notifications") return [];

        return (currentData as InboxNotificationProps[])
            .filter((notification) => notification.isRead === false)
            .map((notification) => notification.id);
    }, [currentData, currentView]);

    const markVisibleNotificationsRead = useCallback(() => {
        void markAllNotificationsRead(unreadNotificationIds);
    }, [markAllNotificationsRead, unreadNotificationIds]);

    const openNotification = useCallback(
        (notification: InboxNotificationProps) => {
            void markNotificationRead(notification.id);

            const target = resolveNotificationDeepLink(
                notification.deepLink,
                notification.scope,
            );
            if (!target) return;

            setPendingScrollToMessageId(
                target.threadRootMessageId ?? target.messageId ?? null,
            );
            router.push(target.href as never);
        },
        [markNotificationRead, setPendingScrollToMessageId],
    );

    const renderItem = useCallback(
        (item: any) => {
            if (currentView === "notifications") {
                const notification = item as InboxNotificationProps;
                return (
                    <View key={notification.id}>
                        <InboxNotification
                            {...notification}
                            onMarkRead={(id) => {
                                void markNotificationRead(id);
                            }}
                            onPress={() => openNotification(notification)}
                        />
                    </View>
                );
            }

            if (currentTab === "chats") {
                const isActive = item.id === params.id;

                return (
                    <View key={item.id}>
                        <InboxChat
                            {...item}
                            isActive={isActive}
                            onPress={() => {
                                router.push(`/chats/${item.id}`);
                            }}
                        />
                    </View>
                );
            }

            // For "subjects" tab we currently render the same chat-row variant.
            // A dedicated subject grouping is tracked separately in a follow-up.
            const isActive = item.id === params.id;

            return (
                <View key={item.id}>
                    <InboxChat
                        {...item}
                        isActive={isActive}
                        onPress={() => {
                            router.push(`/subjects/${item.id}`);
                        }}
                    />
                </View>
            );
        },
        [currentView, currentTab, markNotificationRead, openNotification, params.id]
    );

    return (
        <MasterDetailLayout
            isDetailView={isDetailView}
            sidebar={
                isMobile ? (
                    <InboxMobile
                        data={currentData}
                        isLoading={isLoading}
                        renderItem={renderItem}
                        notificationUnreadCount={unreadNotificationIds.length}
                        isMarkingAllNotificationsRead={isMarkingAllNotificationsRead}
                        onMarkAllNotificationsRead={markVisibleNotificationsRead}
                    />
                ) : (
                    <Inbox
                        data={currentData}
                        isLoading={isLoading}
                        renderItem={renderItem}
                        notificationUnreadCount={unreadNotificationIds.length}
                        isMarkingAllNotificationsRead={isMarkingAllNotificationsRead}
                        onMarkAllNotificationsRead={markVisibleNotificationsRead}
                    />
                )
            }
        />
    );
};
