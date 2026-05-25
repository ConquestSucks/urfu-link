import React from "react";
import { Pressable, Text, View } from "react-native";
import { ChecksIcon } from "@/shared/ui/phosphor";

interface NotificationListToolbarProps {
    unreadCount: number;
    isMarkingAllRead?: boolean;
    onMarkAllRead?: () => void;
}

export const NotificationListToolbar = ({
    unreadCount,
    isMarkingAllRead = false,
    onMarkAllRead,
}: NotificationListToolbarProps) => {
    const canMarkAll = unreadCount > 0 && !isMarkingAllRead && Boolean(onMarkAllRead);

    return (
        <View className="px-3">
            <View className="flex-row items-center justify-between gap-3 rounded-lg border border-white/[0.05] bg-white/[0.025] px-3 py-2">
                <Text
                    numberOfLines={1}
                    className="text-xs font-medium text-text-subtle flex-1"
                >
                    {unreadCount > 0
                        ? `Непрочитанных: ${unreadCount}`
                        : "Все уведомления прочитаны"}
                </Text>

                <Pressable
                    accessibilityRole="button"
                    accessibilityLabel="Прочитать все уведомления"
                    accessibilityState={{ disabled: !canMarkAll }}
                    disabled={!canMarkAll}
                    onPress={onMarkAllRead}
                    className={`h-8 flex-row items-center gap-1.5 rounded-lg px-2.5 transition-colors duration-200 ${
                        canMarkAll
                            ? "bg-brand-600 hover:bg-brand-500 active:bg-brand-700"
                            : "bg-white/[0.04] opacity-60"
                    }`}
                >
                    <ChecksIcon
                        size={16}
                        weight="bold"
                        className={canMarkAll ? "text-white" : "text-text-subtle"}
                    />
                    <Text
                        className={`text-xs font-semibold ${
                            canMarkAll ? "text-white" : "text-text-subtle"
                        }`}
                    >
                        {isMarkingAllRead ? "Читаем..." : "Прочитать все"}
                    </Text>
                </Pressable>
            </View>
        </View>
    );
};
