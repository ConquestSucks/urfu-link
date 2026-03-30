import { Avatar } from "@/shared/ui";
import { ChecksIcon } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, Text, View } from "react-native";
import { InboxChatProps } from "../model/types";

export const InboxChat = ({
    avatarUrl,
    name,
    message,
    time,
    isActive,
    onPress,
    unreadCount = 0,
    lastMessageFromSelf,
    lastMessageRead,
}: InboxChatProps) => {
    const hasUnread = unreadCount > 0;
    const showChecks = Boolean(lastMessageFromSelf);

    return (
        <Pressable className="select-none" onPress={onPress}>
            <View
                className={`px-4 py-3 flex-row items-center gap-3 md:rounded-2xl active:bg-brand-600/10 transition-all duration-300 ${isActive ? "bg-brand-600/10" : "hover:bg-white/5"}`}
            >
                <Avatar size={48} src={avatarUrl} />

                <View className="flex-1 gap-2 min-w-0">
                    <View className="flex-row justify-between items-start gap-2">
                        <Text numberOfLines={1} className="leading-none flex-1 text-base font-semibold text-white">
                            {name}
                        </Text>
                        <Text className="leading-none text-xs font-medium text-text-subtle">
                            {time}
                        </Text>
                    </View>

                    <View className="flex-row items-center gap-2 min-w-0">
                        <View className="flex-row items-center gap-1.5 flex-1 min-w-0">
                            {showChecks && (
                                <ChecksIcon
                                    size={16}
                                    className={lastMessageRead ? "text-brand-600" : "text-text-placeholder"}
                                    weight="bold"
                                />
                            )}
                            <Text
                                numberOfLines={1}
                                className={`leading-none text-sm font-medium flex-1 min-w-0 text-text-subtle`}
                            >
                                {message}
                            </Text>
                        </View>
                        {hasUnread && (
                            <View className="bg-brand-600 min-w-[20px] h-5 px-1.5 rounded-full items-center justify-center shrink-0">
                                <Text className="text-white text-[10px] font-bold">
                                    {unreadCount > 99 ? "99+" : String(unreadCount)}
                                </Text>
                            </View>
                        )}
                    </View>
                </View>
            </View>
        </Pressable>
    );
};
