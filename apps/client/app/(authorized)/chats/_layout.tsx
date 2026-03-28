import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification } from "@/entities/inbox-notification";
import { MasterDetailLayout } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import { InboxSidebar, TabType } from "@/widgets/inbox-sidebar";
import { router, useGlobalSearchParams } from "expo-router";
import { useEffect, useState } from "react";
import { Platform } from "react-native";

export default function ChatsLayout() {
  const { id: currentChatId } = useGlobalSearchParams<{ id: string }>();
  const isWeb = Platform.OS === "web";

  const [activeTab, setActiveTab] = useState<TabType>("chats");

  const chatsData = useInboxStore((state) => state.chats);
  const notificationsData = useInboxStore((state) => state.notifications);

  const isChatsLoading = useInboxStore((state) => state.isChatsLoading);
  const isNotificationsLoading = useInboxStore(
    (state) => state.isNotificationsLoading,
  );
  const fetchChats = useInboxStore((state) => state.fetchChats);
  const fetchNotifications = useInboxStore((state) => state.fetchNotifications);

  const currentTitle = activeTab === "chats" ? "Личные чаты" : "Уведомления";
  const currentData = activeTab === "chats" ? chatsData : notificationsData;
  const currentIsLoading =
    activeTab === "chats" ? isChatsLoading : isNotificationsLoading;

  useEffect(() => {
    if (activeTab === "chats") {
      fetchChats();
    } else if (activeTab === "notifications") {
      fetchNotifications();
    }
  }, [activeTab]);

  return (
    <MasterDetailLayout
      sidebar={
        <InboxSidebar
          title={currentTitle}
          activeTab={activeTab}
          onTabChange={setActiveTab}
          data={currentData}
          isLoading={currentIsLoading}
          variant="chats"
          renderItem={(message: any) => {
            if (activeTab === "chats") {
              return (
                <InboxChat
                  key={message.id}
                  id={message.id}
                  avatarUrl={message.avatarUrl}
                  name={message.name}
                  message={message.message}
                  time={message.time}
                  isActive={message.id === currentChatId}
                  onPress={() => {
                    if (isWeb) router.replace(`/chats/${message.id}`);
                    else router.push(`/chats/${message.id}`);
                  }}
                />
              );
            }
            return <InboxNotification key={message.id} {...message} />;
          }}
        />
      }
    />
  );
}
