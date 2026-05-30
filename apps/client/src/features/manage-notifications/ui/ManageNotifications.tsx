import { useCurrentUser, useUpdateNotifications } from "@/entities/user";
import { Skeleton, SwitchCard, SwitchCardSkeleton } from "@/shared/ui";
import { Platform, ScrollView, Text, View } from "react-native";
import { useEffect, useState } from "react";
import {
  getBrowserNotificationPermission,
  requestBrowserNotificationPermission,
} from "@/shared/lib/browser-notifications";
import { NOTIFICATIONS_SETTINGS, type NotificationField } from "../config/settings";

export const ManageNotifications = () => {
  const { data: profile, isLoading } = useCurrentUser();
  const updateNotifications = useUpdateNotifications();
  const [browserPermission, setBrowserPermission] = useState<
    NotificationPermission | "unsupported"
  >("unsupported");
  const [isPermissionRequesting, setIsPermissionRequesting] = useState(false);

  useEffect(() => {
    if (Platform.OS !== "web") return;
    setBrowserPermission(getBrowserNotificationPermission());
  }, []);

  if (isLoading) {
    return (
      <ScrollView contentContainerClassName="gap-4" showsVerticalScrollIndicator={false}>
        {Object.values(NOTIFICATIONS_SETTINGS).map((section, sectionIndex) => (
          <View key={sectionIndex} className="gap-3">
            <Skeleton className="h-3 w-28 rounded" />
            <View className="gap-3">
              {section.items.map((item) => (
                <SwitchCardSkeleton key={item.key} />
              ))}
            </View>
          </View>
        ))}
      </ScrollView>
    );
  }

  const notifications = profile?.notifications;

  const handleToggle = (field: NotificationField) => (newValue: boolean) => {
    if (!notifications) return;
    updateNotifications.mutate({
      newMessages: notifications.newMessages,
      notificationSound: field === "notificationSound" ? newValue : notifications.notificationSound,
      disciplineChatMessages: field === "disciplineChatMessages" ? newValue : notifications.disciplineChatMessages,
      mentions: field === "mentions" ? newValue : notifications.mentions,
    });
  };

  const handleBrowserPermissionRequest = async () => {
    if (browserPermission !== "default") return;
    setIsPermissionRequesting(true);
    try {
      setBrowserPermission(await requestBrowserNotificationPermission());
    } finally {
      setIsPermissionRequesting(false);
    }
  };

  return (
    <ScrollView contentContainerClassName="gap-4" showsVerticalScrollIndicator={false}>
      {Platform.OS === "web" && browserPermission === "default" && (
        <View className="gap-3">
          <Text className="text-text-placeholder text-xs font-semibold uppercase">
            Браузер
          </Text>
          <SwitchCard
            label="Браузерные уведомления"
            description="Показывать уведомления, когда вкладка не активна"
            value={false}
            onValueChange={handleBrowserPermissionRequest}
            disabled={isPermissionRequesting}
          />
        </View>
      )}
      {Object.values(NOTIFICATIONS_SETTINGS).map((section, sectionIndex) => (
        <View key={sectionIndex} className="gap-3">
          <Text className="text-text-placeholder text-xs font-semibold uppercase">
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
