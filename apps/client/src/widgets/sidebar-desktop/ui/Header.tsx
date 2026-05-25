import { AnimatedView } from "@/shared/lib/nativewind-interop";
import { AnimatedViewStyle } from "@/shared/types";
import { Logo } from "@/shared/ui";
import { useNotificationBadge } from "@/features/notifications";
import { useNotificationStore } from "@/shared/store/notification-store";
import { NotificationBell } from "@/widgets/notifications";
import type { Href } from "expo-router";
import { router } from "expo-router";
import { Text, View } from "react-native";
interface HeaderProps {
    textAnimatedStyle: AnimatedViewStyle;
}
export const Header = ({ textAnimatedStyle }: HeaderProps) => {
    const { data } = useNotificationBadge();
    const liveBadge = useNotificationStore((s) => s.badge);
    const badge = liveBadge ?? data;

    return (<View className="flex justify-start w-full py-[28px] px-6 flex-row gap-3 items-center">
      <Logo size={39}/>
      <AnimatedView style={textAnimatedStyle}>
        <Text numberOfLines={1} className="text-white text-2xl font-extrabold tracking-tight">
          URFU LINK
        </Text>
      </AnimatedView>
      <View className="ml-auto">
        <NotificationBell
          unreadCount={badge?.total ?? 0}
          unseenCount={badge?.totalUnseen ?? 0}
          onPress={() => router.push("/notifications" as Href)}
        />
      </View>
    </View>);
};
