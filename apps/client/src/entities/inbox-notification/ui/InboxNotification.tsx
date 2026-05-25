import React, { useCallback, useEffect, useRef } from "react";
import { Pressable, Text, View } from "react-native";
import { InboxNotificationProps } from "../model/types";
import { InfoIcon } from "@/shared/ui/phosphor";

const HOVER_READ_DELAY_MS = 2_000;

export const InboxNotification = ({
    id,
    title,
    time,
    description,
    actorName,
    isRead = true,
    onMarkRead,
    onPress,
}: InboxNotificationProps) => {
    const unread = isRead === false;
    const titleText = actorName?.trim()
        ? `${actorName.trim()} · ${title}`
        : title;
    const readTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const hasScheduledReadRef = useRef(false);

    const clearHoverTimer = useCallback(() => {
        if (readTimerRef.current === null) return;
        clearTimeout(readTimerRef.current);
        readTimerRef.current = null;
    }, []);

    const scheduleRead = useCallback(() => {
        if (!unread || !onMarkRead || hasScheduledReadRef.current) return;

        clearHoverTimer();
        readTimerRef.current = setTimeout(() => {
            hasScheduledReadRef.current = true;
            readTimerRef.current = null;
            onMarkRead(id);
        }, HOVER_READ_DELAY_MS);
    }, [clearHoverTimer, id, onMarkRead, unread]);

    const handlePress = useCallback(() => {
        clearHoverTimer();
        onPress?.();
    }, [clearHoverTimer, onPress]);

    useEffect(() => {
        if (unread) {
            hasScheduledReadRef.current = false;
            return;
        }

        clearHoverTimer();
    }, [clearHoverTimer, id, unread]);

    useEffect(() => clearHoverTimer, [clearHoverTimer]);

    return (
        <Pressable
            testID="inbox-notification-row"
            onHoverIn={scheduleRead}
            onHoverOut={clearHoverTimer}
            onPress={handlePress}
            accessibilityRole={onPress ? "button" : undefined}
            accessibilityLabel={unread ? `${titleText}. Непрочитано` : titleText}
            accessibilityState={{ selected: unread }}
            className={`flex-row gap-2.5 px-3 py-2.5 md:rounded-lg select-none border-b md:border transition-colors duration-200 ${
                unread
                    ? "border-white/[0.08] bg-white/[0.07] hover:bg-white/[0.09]"
                    : "border-white/[0.04] md:bg-white/[0.025] hover:bg-white/[0.05]"
            }`}
        >
            <View
                className={`mt-0.5 flex-shrink-0 flex items-center justify-center border shadow-sm w-8 h-8 rounded-lg ${
                    unread
                        ? "bg-brand-600/15 border-brand-300/20"
                        : "bg-app-elevated border-white/[0.08]"
                }`}
            >
                <InfoIcon
                    size={17}
                    className={unread ? "text-brand-300" : "text-text-subtle"}
                    weight="bold"
                />
            </View>

            <View className="gap-1 flex-1 min-w-0">
                <View className="flex-row justify-between items-center gap-2">
                    <View className="flex-row items-center gap-1.5 flex-1 min-w-0">
                        {unread && (
                            <View
                                testID="inbox-notification-unread-marker"
                                className="h-1.5 w-1.5 rounded-full bg-brand-300 shrink-0"
                            />
                        )}
                        <Text
                            numberOfLines={1}
                            className={`leading-none text-sm flex-1 min-w-0 ${
                                unread
                                    ? "text-white font-semibold"
                                    : "text-text-secondary font-medium"
                            }`}
                        >
                            {titleText}
                        </Text>
                    </View>
                    <Text className="text-[11px] font-medium text-text-subtle shrink-0">
                        {time}
                    </Text>
                </View>
                <Text
                    numberOfLines={2}
                    className={`text-xs leading-4 mr-1 ${
                        unread ? "text-slate-300" : "text-text-muted"
                    }`}
                >
                    {description}
                </Text>
            </View>
        </Pressable>
    );
};
