import React from "react";
import { Pressable, Text, View } from "react-native";
import { BellIcon } from "@/shared/ui/phosphor";

type NotificationBellProps = {
    unreadCount: number;
    unseenCount: number;
    onPress: () => void;
};

const formatCount = (count: number) => (count > 99 ? "99+" : String(count));

export const NotificationBell = ({ unreadCount, unseenCount, onPress }: NotificationBellProps) => {
    const hasUnread = unreadCount > 0;
    const hasUnseen = unseenCount > 0;

    return (
        <Pressable
            accessibilityRole="button"
            accessibilityLabel="Открыть уведомления"
            onPress={onPress}
            hitSlop={8}
            className="relative h-11 w-11 items-center justify-center rounded-xl bg-white/5 border border-white/5"
        >
            <BellIcon
                size={22}
                weight={hasUnread ? "fill" : "regular"}
                className={hasUnread ? "text-white" : "text-text-muted"}
            />
            {hasUnread && (
                <View className="absolute -right-1 -top-1 min-w-[22px] h-[22px] rounded-full bg-red-500 px-1.5 items-center justify-center border border-app-bg">
                    <Text className="text-[10px] leading-none font-bold text-white">
                        {formatCount(unreadCount)}
                    </Text>
                </View>
            )}
            {hasUnseen && !hasUnread && (
                <View className="absolute right-2 top-2 h-2.5 w-2.5 rounded-full bg-red-500 border border-app-bg" />
            )}
        </Pressable>
    );
};
