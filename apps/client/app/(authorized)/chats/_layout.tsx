import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification } from "@/entities/inbox-notification";
import { InboxSubjectGroup } from "@/entities/inbox-subject";
import { navigateInboxPath } from "@/shared/lib/inboxNavigate";
import { useInboxPathIds } from "@/shared/lib/useInboxPathIds";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { MasterDetailLayout } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import { InboxSidebar, InboxSidebarMobile, TabType } from "@/widgets/inbox-sidebar";
import { router } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { Platform, View } from "react-native";

export default function ChatsLayout() {
    const { chatId, subjectThreadId } = useInboxPathIds();
    const isWeb = Platform.OS === "web";
    const { isMobile } = useWindowSize();
    const [desktopListTab, setDesktopListTab] = useState<"chats" | "notifications">("chats");
    const chatsData = useInboxStore((state) => state.chats);
    const subjectsData = useInboxStore((state) => state.subjects);
    const notificationsData = useInboxStore((state) => state.notifications);
    const isChatsLoading = useInboxStore((state) => state.isChatsLoading);
    const isSubjectsLoading = useInboxStore((state) => state.isSubjectsLoading);
    const isNotificationsLoading = useInboxStore((state) => state.isNotificationsLoading);
    const fetchChats = useInboxStore((state) => state.fetchChats);
    const fetchSubjects = useInboxStore((state) => state.fetchSubjects);
    const fetchNotifications = useInboxStore((state) => state.fetchNotifications);
    const mobileInboxTab = useInboxStore((state) => state.mobileInboxTab);
    const setMobileInboxTab = useInboxStore((state) => state.setMobileInboxTab);
    const mobileInboxListMode = useInboxStore((state) => state.mobileInboxListMode);
    const setMobileInboxListMode = useInboxStore((state) => state.setMobileInboxListMode);

    useEffect(() => {
        if (!isMobile) {
            if (desktopListTab === "chats")
                fetchChats();
            else
                fetchNotifications();
            return;
        }
        if (mobileInboxListMode === "messages") {
            if (mobileInboxTab === "chats")
                fetchChats();
            else
                fetchSubjects();
        }
        else {
            fetchNotifications();
        }
    }, [isMobile, mobileInboxTab, mobileInboxListMode, desktopListTab, fetchChats, fetchSubjects, fetchNotifications]);

    const currentTitle = !isMobile && desktopListTab === "notifications"
        ? "Уведомления"
        : "Личные чаты";

    const currentData = useMemo(() => {
        if (!isMobile) {
            return desktopListTab === "chats" ? chatsData : notificationsData;
        }
        if (mobileInboxListMode === "notifications") {
            return notificationsData.filter((n) => n.scope === mobileInboxTab);
        }
        return mobileInboxTab === "chats" ? chatsData : subjectsData;
    }, [isMobile, desktopListTab, mobileInboxListMode, mobileInboxTab, chatsData, subjectsData, notificationsData]);

    const currentIsLoading = !isMobile
        ? desktopListTab === "chats"
            ? isChatsLoading
            : isNotificationsLoading
        : mobileInboxListMode === "notifications"
            ? isNotificationsLoading
            : mobileInboxTab === "chats"
                ? isChatsLoading
                : isSubjectsLoading;

    const handleDesktopTabChange = (tab: TabType) => {
        if (tab === "chats" || tab === "notifications") {
            setDesktopListTab(tab);
        }
    };

    const handleMobileTabChange = (tab: TabType) => {
        if (tab === "chats" || tab === "subjects") {
            setMobileInboxTab(tab);
        }
    };

    const mobileSidebarTab: TabType = mobileInboxTab === "subjects" ? "subjects" : "chats";
    const desktopSidebarTab: TabType = desktopListTab === "notifications" ? "notifications" : "chats";

    const commonProps = {
        activeTab: isMobile ? mobileSidebarTab : desktopSidebarTab,
        onTabChange: isMobile ? handleMobileTabChange : handleDesktopTabChange,
        data: currentData,
        isLoading: currentIsLoading,
        variant: "chats" as const,
        renderItem: (item: any) => {
            if (isMobile) {
                if (mobileInboxListMode === "notifications") {
                    return (
                        <View key={item.id}>
                            <InboxNotification {...item} />
                        </View>
                    );
                }
                if (mobileInboxTab === "chats") {
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
                            subject={item}
                            activeChatId={subjectThreadId}
                            onChatPress={(id) => navigateInboxPath(router, isWeb, `/subjects/${id}`)}
                        />
                    </View>
                );
            }
            if (desktopListTab === "chats") {
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
                    <InboxNotification {...item} />
                </View>
            );
        },
    };

    return (
        <MasterDetailLayout
            sidebar={isMobile
                ? (
                    <InboxSidebarMobile
                        {...commonProps}
                        listMode={mobileInboxListMode}
                        onListModeChange={setMobileInboxListMode}
                    />
                )
                : (
                    <InboxSidebar title={currentTitle} {...commonProps} />
                )}
        />
    );
}
