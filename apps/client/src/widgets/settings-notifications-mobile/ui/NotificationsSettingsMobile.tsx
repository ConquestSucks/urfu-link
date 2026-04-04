import { useCurrentUser, useUpdateNotifications } from "@/entities/user";
import { safeGoBack } from "@/shared/lib/safeGoBack";
import { MOBILE_TAB_BAR_HEIGHT } from "@/widgets/bottom-tabs-mobile/config/layout";
import { SwitchCard } from "@/shared/ui";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import { ActivityIndicator, Pressable, ScrollView, Text, View } from "react-native";

type NotificationField = "newMessages" | "notificationSound" | "disciplineChatMessages" | "mentions";

export const NotificationsSettingsMobile = () => {
    const { data: profile, isLoading } = useCurrentUser();
    const updateNotifications = useUpdateNotifications();

    if (isLoading) {
        return (
            <View className="flex-1 bg-app-bg items-center justify-center">
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
        <View className="flex-1 bg-app-bg">
            <View className="flex-row items-center px-6 py-8 border-b border-white/5">
                <Pressable onPress={() => safeGoBack("/profile")} className="mr-6" hitSlop={8}>
                    <CaretLeftIcon size={24} className="text-white" />
                </Pressable>
                <Text className="text-white text-2xl font-bold">Уведомления</Text>
            </View>

            <ScrollView
                className="flex-1"
                contentContainerStyle={{
                    padding: 24,
                    paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT,
                    gap: 16,
                }}
                showsVerticalScrollIndicator={false}
            >
                <Text className="text-text-placeholder text-xs font-bold uppercase tracking-wide">
                    Личные чаты
                </Text>
                <SwitchCard
                    label="Уведомления о новых сообщениях"
                    description="Получать уведомления при получении личных сообщений"
                    value={notifications?.newMessages ?? true}
                    onValueChange={handleToggle("newMessages")}
                    disabled={updateNotifications.isPending}
                />
                <SwitchCard
                    label="Звук уведомлений"
                    description="Воспроизводить звук при новом сообщении"
                    value={notifications?.notificationSound ?? true}
                    onValueChange={handleToggle("notificationSound")}
                    disabled={updateNotifications.isPending}
                />

                <Text className="text-text-placeholder text-xs font-bold uppercase tracking-wide mt-2">
                    Дисциплины
                </Text>
                <SwitchCard
                    label="Уведомления от дисциплин"
                    description="Получать уведомления о сообщениях в чатах дисциплин"
                    value={notifications?.disciplineChatMessages ?? true}
                    onValueChange={handleToggle("disciplineChatMessages")}
                    disabled={updateNotifications.isPending}
                />
                <SwitchCard
                    label="Упоминания"
                    description="Уведомлять только когда меня упоминают"
                    value={notifications?.mentions ?? false}
                    onValueChange={handleToggle("mentions")}
                    disabled={updateNotifications.isPending}
                />

                <View className="bg-zinc-900 rounded-2xl p-5 border border-white/5 mt-2">
                    <Text className="text-text-placeholder text-sm leading-5">
                        Настройте уведомления так, чтобы не пропустить важные учебные события и
                        сообщения от одногруппников, сохраняя при этом фокус на учебе.
                    </Text>
                </View>
            </ScrollView>
        </View>
    );
};
