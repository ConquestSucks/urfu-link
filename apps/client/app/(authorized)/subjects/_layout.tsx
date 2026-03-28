import { InboxChat } from "@/entities/inbox-chat";
import { InboxNotification } from "@/entities/inbox-notification";
import { MasterDetailLayout } from "@/shared/ui";
import { useInboxStore } from "@/store/useInboxStore";
import { InboxSidebar, TabType } from "@/widgets/inbox-sidebar";
import { router, useGlobalSearchParams } from "expo-router";
import { useEffect, useState } from "react";
import { Platform, Text, View } from "react-native";

export default function SubjectsLayout() {
  const { id: currentSubjectId } = useGlobalSearchParams<{ id: string }>();
  const isWeb = Platform.OS === "web";

  const [activeTab, setActiveTab] = useState<TabType>("chats");

  const subjectsData = useInboxStore((state) => state.subjects);
  const notificationsData = useInboxStore((state) => state.notifications);

  const isSubjectsLoading = useInboxStore((state) => state.isSubjectsLoading);
  const isNotificationsLoading = useInboxStore(
    (state) => state.isNotificationsLoading,
  );

  const fetchSubjects = useInboxStore((state) => state.fetchSubjects);
  const fetchNotifications = useInboxStore((state) => state.fetchNotifications);

  const currentTitle = activeTab === "chats" ? "Дисциплины" : "Уведомления";
  const currentData = activeTab === "chats" ? subjectsData : notificationsData;

  const currentIsLoading =
    activeTab === "chats" ? isSubjectsLoading : isNotificationsLoading;

  useEffect(() => {
    if (activeTab === "chats") {
      fetchSubjects();
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
          variant="subjects"
          renderItem={(subject: any) => {
            if (activeTab === "chats") {
              return (
                <View key={subject.id} className="gap-1">
                  <View className="h-12 px-4 justify-center">
                    <Text className="text-[10px] uppercase text-[#62748E] font-bold">
                      {subject.title}
                    </Text>
                  </View>
                  <View className="gap-2">
                    {subject.messages.map((message: any) => (
                      <InboxChat
                        key={message.id}
                        id={message.id}
                        avatarUrl={message.avatarUrl}
                        name={message.name}
                        message={message.message}
                        time={message.time}
                        isActive={message.id === currentSubjectId}
                        onPress={() => {
                          if (isWeb) router.replace(`/subjects/${message.id}`);
                          else router.push(`/subjects/${message.id}`);
                        }}
                      />
                    ))}
                  </View>
                </View>
              );
            }
            return <InboxNotification key={subject.id} {...subject} />;
          }}
        />
      }
    />
  );
}
