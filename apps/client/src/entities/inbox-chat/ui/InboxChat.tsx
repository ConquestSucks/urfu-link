import { Avatar } from "@/shared/ui";
import { CheckIcon, ChecksIcon } from "@/shared/ui/phosphor";
import { TypingEllipsis } from "@/shared/ui/TypingEllipsis";
import React from "react";
import { Pressable, Text, View } from "react-native";
import { InboxChatProps } from "../model/types";

const InboxTypingPreview = ({
    message,
    className,
}: {
    message: string;
    className: string;
}) => (
    <View testID="inbox-typing-preview" className="flex-row items-center flex-1 min-w-0">
        <Text
            numberOfLines={1}
            className={`leading-none text-sm flex-shrink min-w-0 ${className}`}
        >
            {message}
        </Text>
        <TypingEllipsis
            testID="inbox-typing-dots"
            className="ml-1 shrink-0 text-brand-300 text-sm font-bold leading-none"
        />
    </View>
);

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
    isTyping = false,
}: InboxChatProps) => {
    const hasUnread = unreadCount > 0;
    const showTypingPreview = isTyping;
    const showChecks = Boolean(lastMessageFromSelf) && !showTypingPreview;
    const containerClass = isActive
        ? "bg-brand-600/10"
        : hasUnread
          ? "bg-white/[0.08] hover:bg-white/[0.10]"
          : "hover:bg-white/5";
    const messageClass = showTypingPreview
        ? "text-brand-300 font-semibold"
        : hasUnread
        ? "text-white font-semibold"
        : "text-text-subtle font-medium";
    const timeClass = hasUnread
        ? "text-brand-300 font-semibold"
        : "text-text-subtle font-medium";
    const previewMessage = showTypingPreview && !message.trim()
        ? "Печатает"
        : message;

    return (
        <Pressable className="select-none" onPress={onPress}>
            <View
                className={`px-4 py-3 flex-row items-center gap-3 md:rounded-2xl active:bg-brand-600/10 transition-all duration-300 ${containerClass}`}
            >
                <Avatar size={48} src={avatarUrl} name={name} />

                <View className="flex-1 gap-2 min-w-0">
                    <View className="flex-row justify-between items-start gap-2">
                        <Text numberOfLines={1} className="leading-none flex-1 text-base font-semibold text-white">
                            {name}
                        </Text>
                        <Text className={`leading-none text-xs ${timeClass}`}>
                            {time}
                        </Text>
                    </View>

                    <View className="flex-row items-center gap-2 min-w-0">
                        <View className="flex-row items-center gap-1.5 flex-1 min-w-0">
                            {showTypingPreview ? (
                                <InboxTypingPreview
                                    message={previewMessage}
                                    className={messageClass}
                                />
                            ) : (
                                <Text
                                    numberOfLines={1}
                                    className={`leading-none text-sm flex-1 min-w-0 ${messageClass}`}
                                >
                                    {message}
                                </Text>
                            )}
                            {showChecks && (
                                lastMessageRead ? (
                                    <ChecksIcon
                                        size={16}
                                        className="text-brand-600 shrink-0"
                                        weight="bold"
                                    />
                                ) : (
                                    <CheckIcon
                                        size={16}
                                        className="text-text-placeholder shrink-0"
                                        weight="bold"
                                    />
                                )
                            )}
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
