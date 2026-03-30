import { useCurrentUser, useUpdateNotifications } from "@/entities/user";
import { SwitchCard } from "@/shared/ui";
import { ActivityIndicator, ScrollView, Text, View } from "react-native";
import { NOTIFICATIONS_SETTINGS, type NotificationField } from "../config/settings";

export const ManageNotifications = () => {
  const { data: profile, isLoading } = useCurrentUser();
  const updateNotifications = useUpdateNotifications();

  if (isLoading) {
    return (
      <View className="flex-1 items-center justify-center">
        <ActivityIndicator />
      </View>
    );
  }

  const notifications = profile?.notifications;

  const handleToggle = (field: NotificationField) => (newValue: boolean) => {
    if (!notifications) return;
    updateNotifications.mutate({
      newMessages: field === "newMessages" ? newValue : notifications.newMessages,
      notificationSound: field === "notificationSound" ? newValue : notifications.notificationSound,
      disciplineChatMessages: field === "disciplineChatMessages" ? newValue : notifications.disciplineChatMessages,
      mentions: field === "mentions" ? newValue : notifications.mentions,
    });
  };

  return (
    <ScrollView contentContainerClassName="gap-4" showsVerticalScrollIndicator={false}>
      {Object.values(NOTIFICATIONS_SETTINGS).map((section, sectionIndex) => (
        <View key={sectionIndex} className="gap-3">
          <Text className="text-text-secondary text-sm font-semibold">
            {section.label}
          </Text>
          <View className="gap-3">
            {section.items.map((item) => (
              <SwitchCard
                key={item.key}
                label={item.label}
                description={item.description}
                value={notifications?.[item.key as NotificationField] ?? true}
                onValueChange={handleToggle(item.key as NotificationField)}
                disabled={updateNotifications.isPending}
              />
            ))}
          </View>
        </View>
      ))}
    </ScrollView>
  );
};
