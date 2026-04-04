import { useCallback, useEffect, useMemo } from "react";
import { View } from "react-native";
import { router, useSegments } from "expo-router";

import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification } from "@/entities/inbox-notification";
import { InboxSubjectGroup, type InboxSubjectProps } from "@/entities/inbox-subject";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { MasterDetailLayout } from "@/shared/ui";
import { useInboxData, useInboxActions } from "@/shared/store/useInboxStore";
import { Inbox } from "./Inbox";
import { InboxMobile } from "./InboxMobile";

export const InboxLayout = () => {
    const segments = useSegments() as string[];
    const { isMobile } = useWindowSize();

    const { currentTab, currentView, params } = useInboxRouting();

    const {
        chats,
        subjects,
        notifications,
        isChatsLoading,
        isSubjectsLoading,
        isNotificationsLoading,
    } = useInboxData();

    const { fetchChats, fetchSubjects, fetchNotifications } = useInboxActions();

    const isDetailView = segments.includes("[id]");

    useEffect(() => {
        if (!params.view) {
            router.setParams({ view: "messages" });
        }
    }, [params.view]);

    useEffect(() => {
        if (currentView === "notifications") {
            fetchNotifications();
        } else {
            if (currentTab === "chats") fetchChats();
            if (currentTab === "subjects") fetchSubjects();
        }
    }, [currentView, currentTab, fetchChats, fetchSubjects, fetchNotifications]);

    const currentData = useMemo(() => {
        if (currentView === "notifications") {
            return notifications.filter((n) => n.scope === currentTab);
        }
        return currentTab === "chats" ? chats : subjects;
    }, [currentView, currentTab, chats, subjects, notifications]);

    const isLoading =
        currentView === "notifications"
            ? isNotificationsLoading
            : currentTab === "chats"
              ? isChatsLoading
              : isSubjectsLoading;

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

            return (
                <View key={item.id}>
                    <InboxSubjectGroup
                        subject={item as InboxSubjectProps}
                        activeChatId={params.id}
                        onChatPress={(id) => router.push(`/subjects/${id}`)}
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