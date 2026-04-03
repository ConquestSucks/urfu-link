import { ManageMedia } from "@/features/manage-media";
import { safeGoBack } from "@/shared/lib/safeGoBack";
import { MOBILE_TAB_BAR_HEIGHT } from "@/widgets/bottom-tabs-mobile/config/layout";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import { Pressable, Text, View } from "react-native";

export const MediaSettingsMobile = () => {
    return (
        <View className="flex-1 bg-app-bg">
            <View className="flex-row items-center px-6 py-8 border-b border-white/5">
                <Pressable onPress={() => safeGoBack("/profile")} className="mr-6">
                    <CaretLeftIcon size={24} className="text-white" />
                </Pressable>
                <Text className="text-white text-2xl font-bold">Звук и видео</Text>
            </View>

            <View
                className="flex-1 px-6"
                style={{ paddingBottom: 24 + MOBILE_TAB_BAR_HEIGHT }}
            >
                <ManageMedia />
            </View>
        </View>
    );
};
