import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification } from "@/entities/inbox-notification";
import { InboxSubjectGroup, type InboxSubjectProps } from "@/entities/inbox-subject";
import { TabType } from "@/entities/tab";
import { navigateInboxPath } from "@/shared/lib/inboxNavigate";
import { useInboxPathIds } from "@/shared/lib/useInboxPathIds";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { MasterDetailLayout } from "@/shared/ui";
import { useInboxData, useInboxActions, useTabState, useViewState } from "@/store/useInboxStore";
import { Inbox, InboxMobile } from "@/widgets/inbox";
import { router, useSegments } from "expo-router";
import { useEffect, useMemo } from "react";
import { Platform, View } from "react-native";

export default function SubjectsLayout() {
    const [currentTab, setCurrentTab] = useTabState();
    const [currentView, setCurrentView] = useViewState();

    const segments = useSegments() as string[];
    const { chatId, subjectThreadId } = useInboxPathIds();
    const isWeb = Platform.OS === "web";
    const { isMobile } = useWindowSize();

    const {
        chats,
        subjects,
        notifications,
        isChatsLoading,
        isSubjectsLoading,
        isNotificationsLoading,
    } = useInboxData();

    const { fetchChats, fetchSubjects, fetchNotifications } = useInboxActions();

    const isDetailView =
        (segments.includes("chats") || segments.includes("subjects")) && segments.includes("[id]");

    useEffect(() => {
        if (currentTab !== "subjects") {
            setCurrentTab("subjects");
        }
    }, [currentTab, setCurrentTab]);

    useEffect(() => {
        if (currentView === "notifications") {
            fetchNotifications(/*currentTab*/);
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

    const handleTabChange = (tab: TabType) => {
        setCurrentTab(tab);
        if (tab === "chats") router.replace("/chats");
        if (tab === "subjects") router.replace("/subjects");
    };

    const renderItem = (item: any) => {
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
                        isActive={item.id === chatId}
                        onPress={() => navigateInboxPath(router, isWeb, `/chats/${item.id}`)}
                    />
                </View>
            );
        }

        return (
            <View key={item.id}>
                <InboxSubjectGroup
                    subject={item as InboxSubjectProps}
                    activeChatId={subjectThreadId}
                    onChatPress={(id) => navigateInboxPath(router, isWeb, `/subjects/${id}`)}
                />
            </View>
        );
    };

    return (
        <MasterDetailLayout
            isDetailView={isDetailView}
            sidebar={
                isMobile ? (
                    <InboxMobile
                        data={currentData}
                        isLoading={isLoading}
                        renderItem={renderItem}
                        currentTab={currentTab}
                        onTabChange={handleTabChange}
                        currentView={currentView}
                        onCurrentViewChange={setCurrentView}
                    />
                ) : (
                    <Inbox
                        data={currentData}
                        isLoading={isLoading}
                        renderItem={renderItem}
                        currentTab={currentTab}
                        currentView={currentView}
                        onCurrentViewChange={setCurrentView}
                    />
                )
            }
        />
    );
}
