import { useCallback, useEffect, useMemo } from "react";
import { View } from "react-native";
import { router, useSegments } from "expo-router";

import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification } from "@/entities/inbox-notification";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { MasterDetailLayout } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useInboxStore } from "@/shared/store/useInboxStore";
import { useInboxConversations } from "../model/use-inbox-conversations";
import { Inbox } from "./Inbox";
import { InboxMobile } from "./InboxMobile";

export const InboxLayout = () => {
    const segments = useSegments() as string[];
    const { isMobile } = useWindowSize();

    const { currentTab, currentView, params } = useInboxRouting();

    const conversationsLoading = useChatStore((s) => s.isLoading);
    const loadConversations = useChatStore((s) => s.loadConversations);
    const chats = useInboxConversations("chats");
    const subjects = useInboxConversations("subjects");

    const notifications = useInboxStore((s) => s.notifications);
    const isNotificationsLoading = useInboxStore((s) => s.isNotificationsLoading);
    const fetchNotifications = useInboxStore((s) => s.fetchNotifications);

    const isDetailView = segments.includes("[id]");

    useEffect(() => {
        if (!params.view) {
            router.setParams({ view: "messages" });
        }
    }, [params.view]);

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

    const renderItem = useCallback(
        (item: any) => {
            if (currentView === "notifications") {
                return (
                    <View key={item.id}>
                        <InboxNotification {...item} />
                    </View>
                );
            }

            if (currentTab === "chats") {
                return (
                    <View key={item.id}>
                        <InboxChat
                            {...item}
                            isActive={item.id === params.id}
                            onPress={() => router.push(`/chats/${item.id}`)}
                        />
                    </View>
                );
            }

            // For "subjects" tab we currently render the same chat-row variant.
            // A dedicated subject grouping is tracked separately in a follow-up.
            return (
                <View key={item.id}>
                    <InboxChat
                        {...item}
                        isActive={item.id === params.id}
                        onPress={() => router.push(`/subjects/${item.id}`)}
                    />
                </View>
            );
        },
        [currentView, currentTab, params.id]
    );

    return (
        <MasterDetailLayout
            isDetailView={isDetailView}
            sidebar={
                isMobile ? (
                    <InboxMobile data={currentData} isLoading={isLoading} renderItem={renderItem} />
                ) : (
                    <Inbox data={currentData} isLoading={isLoading} renderItem={renderItem} />
                )
            }
        />
    );
};
