import { InboxNotification } from "@/entities/inbox-notification";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useInboxStore } from "@/store/useInboxStore";
import { CaretLeftIcon } from "phosphor-react-native";
import { type Href, router, useFocusEffect } from "expo-router";
import { useCallback, useEffect } from "react";
import { Pressable, ScrollView, Text, View } from "react-native";

export default function NotificationsScreen() {
    const { isMobile } = useWindowSize();
    const notifications = useInboxStore((state) => state.notifications);
    const isLoading = useInboxStore((state) => state.isNotificationsLoading);
    const fetchNotifications = useInboxStore((state) => state.fetchNotifications);
    const setMobileInboxListMode = useInboxStore((state) => state.setMobileInboxListMode);

    useFocusEffect(
        useCallback(() => {
            fetchNotifications();
        }, [fetchNotifications]),
    );

    useEffect(() => {
        if (isMobile) {
            setMobileInboxListMode("notifications");
            router.replace("/chats" as Href);
        }
    }, [isMobile, setMobileInboxListMode]);

    if (isMobile) {
        return null;
    }

    return (
        <View className="flex-1 bg-[#080D1D]">
            <View className="flex-row items-center px-6 py-4 border-b border-white/5">
                <Pressable onPress={() => safeGoBack("/chats")} hitSlop={8}>
                    <CaretLeftIcon size={24} color="#FFFFFF" />
                </Pressable>
                <Text className="text-white text-xl font-bold ml-4">Уведомления</Text>
            </View>

            <ScrollView className="flex-1" contentContainerStyle={{ paddingBottom: 24 }} showsVerticalScrollIndicator={false}>
                {isLoading
                    ? (
                        <Text className="text-[#62748E] text-center py-8">Загрузка…</Text>
                    )
                    : notifications.length === 0
                        ? (
                            <Text className="text-[#62748E] text-center py-8">Нет уведомлений</Text>
                        )
                        : (
                            notifications.map((n) => <InboxNotification key={n.id} {...n} />)
                        )}
            </ScrollView>
        </View>
    );
}
