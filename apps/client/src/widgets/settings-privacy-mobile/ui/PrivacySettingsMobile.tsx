import { useCurrentUser, useUpdatePrivacy } from "@/entities/user";
import { safeGoBack } from "@/shared/lib/safeGoBack";
import { MOBILE_TAB_BAR_HEIGHT } from "@/widgets/bottom-tabs-mobile/config/layout";
import { SwitchCard } from "@/shared/ui";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import { ActivityIndicator, Pressable, ScrollView, Text, View } from "react-native";

export const PrivacySettingsMobile = () => {
    const { data: profile, isLoading } = useCurrentUser();
    const updatePrivacy = useUpdatePrivacy();

    if (isLoading) {
        return (
            <View className="flex-1 bg-app-bg items-center justify-center">
                <ActivityIndicator />
            </View>
        );
    }

    const privacy = profile?.privacy;

    const handleToggle = (field: "showOnlineStatus" | "showLastVisitTime") => (newValue: boolean) => {
        if (!privacy) return;
        updatePrivacy.mutate({
            showOnlineStatus: field === "showOnlineStatus" ? newValue : privacy.showOnlineStatus,
            showLastVisitTime: field === "showLastVisitTime" ? newValue : privacy.showLastVisitTime,
        });
    };

    return (
        <View className="flex-1 bg-app-bg">
            <View className="flex-row items-center px-6 py-8 border-b border-white/5">
                <Pressable onPress={() => safeGoBack("/profile")} className="mr-6">
                    <CaretLeftIcon size={24} className="text-white" />
                </Pressable>
                <Text className="text-white text-2xl font-bold">Приватность</Text>
            </View>

            <ScrollView
                className="flex-1"
                contentContainerStyle={{
                    padding: 24,
                    paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT,
                    gap: 16,
                }}
            >
                <SwitchCard
                    label="Показывать статус онлайн"
                    description="Другие пользователи смогут видеть, когда вы в сети"
                    value={privacy?.showOnlineStatus ?? true}
                    onValueChange={handleToggle("showOnlineStatus")}
                    disabled={updatePrivacy.isPending}
                />

                <SwitchCard
                    label="Показывать время последнего визита"
                    description="Отображение времени последней активности"
                    value={privacy?.showLastVisitTime ?? true}
                    onValueChange={handleToggle("showLastVisitTime")}
                    disabled={updatePrivacy.isPending}
                />

                <View className="bg-zinc-900 rounded-3xl p-6 mt-4">
                    <Text className="text-text-placeholder text-[13px] leading-[20px]">
                        Эти настройки помогут вам контролировать, какую информацию о вас могут
                        видеть другие пользователи URFU LINK. Изменения применяются мгновенно.
                    </Text>
                </View>
            </ScrollView>
        </View>
    );
};
