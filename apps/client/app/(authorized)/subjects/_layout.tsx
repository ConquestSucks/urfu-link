import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification } from "@/entities/inbox-notification";
import { InboxSubjectGroup, type InboxSubjectProps } from "@/entities/inbox-subject";
import { navigateInboxPath } from "@/shared/lib/inboxNavigate";
import { useInboxPathIds } from "@/shared/lib/useInboxPathIds";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { MasterDetailLayout } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import { InboxSidebar, InboxSidebarMobile, TabType } from "@/widgets/inbox-sidebar";
import { router } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { Platform, View } from "react-native";

export default function SubjectsLayout() {
    const { chatId, subjectThreadId } = useInboxPathIds();
    const isWeb = Platform.OS === "web";
    const { isMobile } = useWindowSize();
    const [activeTab, setActiveTab] = useState<TabType>("chats");
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
        if (isMobile) {
            setMobileInboxTab("subjects");
        }
    }, [isMobile, setMobileInboxTab]);

    useEffect(() => {
        if (!isMobile) {
            if (activeTab === "chats")
                fetchSubjects();
            else if (activeTab === "notifications")
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
    }, [isMobile, activeTab, mobileInboxTab, mobileInboxListMode, fetchChats, fetchSubjects, fetchNotifications]);

    const currentTitle = activeTab === "chats" ? "Дисциплины" : "Уведомления";

    const mobileCurrentData = useMemo(() => {
        if (mobileInboxListMode === "notifications") {
            return notificationsData.filter((n) => n.scope === mobileInboxTab);
        }
        return mobileInboxTab === "chats" ? chatsData : subjectsData;
    }, [mobileInboxListMode, mobileInboxTab, chatsData, subjectsData, notificationsData]);

    const mobileCurrentIsLoading = mobileInboxListMode === "notifications"
        ? isNotificationsLoading
        : mobileInboxTab === "chats"
            ? isChatsLoading
            : isSubjectsLoading;

    const handleMobileTabChange = (tab: TabType) => {
        if (tab === "chats") {
            setMobileInboxTab("chats");
            router.replace("/chats");
        }
        else if (tab === "subjects") {
            setMobileInboxTab("subjects");
        }
    };

    const renderSubjectGroup = (subject: InboxSubjectProps) => (
        <InboxSubjectGroup
            subject={subject}
            activeChatId={subjectThreadId}
            onChatPress={(id) => navigateInboxPath(router, isWeb, `/subjects/${id}`)}
        />
    );

    if (isMobile) {
        return (
            <MasterDetailLayout
                sidebar={(
                    <InboxSidebarMobile
                        activeTab={mobileInboxTab === "subjects" ? "subjects" : "chats"}
                        onTabChange={handleMobileTabChange}
                        data={mobileCurrentData}
                        isLoading={mobileCurrentIsLoading}
                        listMode={mobileInboxListMode}
                        onListModeChange={setMobileInboxListMode}
                        renderItem={(item: any) => {
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
                                    {renderSubjectGroup(item)}
                                </View>
                            );
                        }}
                    />
                )}
            />
        );
    }

    const currentData = activeTab === "chats" ? subjectsData : notificationsData;
    const currentIsLoading = activeTab === "chats" ? isSubjectsLoading : isNotificationsLoading;

    return (
        <MasterDetailLayout
            sidebar={(
                <InboxSidebar
                    title={currentTitle}
                    activeTab={activeTab}
                    onTabChange={setActiveTab}
                    data={currentData}
                    isLoading={currentIsLoading}
                    variant="subjects"
                    renderItem={(subject: any) => {
                        if (activeTab === "chats") {
                            return (
                                <View key={subject.id}>
                                    {renderSubjectGroup(subject)}
                                </View>
                            );
                        }
                        return <InboxNotification key={subject.id} {...subject} />;
                    }}
                />
            )}
        />
    );
}
