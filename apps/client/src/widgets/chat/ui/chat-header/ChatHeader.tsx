import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Avatar, StatusIndicator } from "@/shared/ui";
import { useInboxStore } from "@/shared/store/useInboxStore";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import React, { useState } from "react";
import { Pressable, Text, View } from "react-native";
import { ChatHeaderActions } from "./ChatHeaderActions";
import { UserProfileModal } from "./UserProfileModal";
import { useUserPresence, presenceStatusToLabel } from "@/entities/presence";
import type { UserStatus } from "@/shared/ui/StatusIndicator";
import { TypingIndicator } from "@/entities/presence";
import { useConversationTypers } from "@/entities/presence";

const presenceStatusToIndicator = (status?: string): UserStatus => {
    switch (status) {
        case "Online": return "online";
        case "Away": return "away";
        case "DoNotDisturb": return "doNotDisturb";
        default: return "offline";
    }
};

interface ChatHeaderProps {
    chatId: string;
    onOpenSearch?: () => void;
}

export const ChatHeader = ({ chatId, onOpenSearch }: ChatHeaderProps) => {
    const [isProfileOpen, setIsProfileOpen] = useState(false);
    const { isMobile } = useWindowSize();
    const chatMeta = useInboxStore((state) => state.getChatById(chatId));

    const peerPresence = useUserPresence(chatId);
    const typers = useConversationTypers(chatId);

    if (!chatMeta) return null;

    const indicatorStatus = presenceStatusToIndicator(peerPresence?.status);
    const statusLabel = peerPresence
        ? presenceStatusToLabel(peerPresence.status)
        : null;

    return (
        <>
            <View className="flex-row justify-between items-center border-b border-white/5 pl-2.5 pr-3 py-2">
                <View className="flex-row gap-1 items-center flex-1 min-w-0">
                    {isMobile && (
                        <Pressable
                            onPress={() => safeGoBack("/chats")}
                            hitSlop={8}
                            className="p-2 rounded-xl"
                        >
                            <CaretLeftIcon size={24} className="text-text-subtle" weight="bold" />
                        </Pressable>
                    )}
                    <View className="flex-row gap-3 items-center">
                        <View className="relative z-1 p-0.5">
                            <Avatar size={38} src={chatMeta.avatarUrl} name={chatMeta.name} />
                            <StatusIndicator
                                status={indicatorStatus}
                                size={12}
                                className="absolute bottom-0 right-0"
                            />
                        </View>

                        <View className="justify-center flex-1 gap-1.5">
                            <Text
                                numberOfLines={1}
                                className="text-white leading-none text-base font-semibold"
                            >
                                {chatMeta.name}
                            </Text>
                            {typers.length > 0 ? (
                                <TypingIndicator conversationId={chatId} showNames={false} />
                            ) : (
                                <Text
                                    numberOfLines={1}
                                    className={`leading-none text-xs font-medium ${
                                        indicatorStatus === "online"
                                            ? "text-success-600"
                                            : "text-text-muted"
                                    }`}
                                >
                                    {statusLabel ?? "Не в сети"}
                                </Text>
                            )}
                        </View>
                    </View>
                </View>

                <ChatHeaderActions
                    onOpenProfile={() => setIsProfileOpen(true)}
                    onSearchPress={() => onOpenSearch?.()}
                />
            </View>

            <UserProfileModal
                isOpen={isProfileOpen}
                onClose={() => setIsProfileOpen(false)}
                user={{ name: chatMeta.name, avatarUrl: chatMeta.avatarUrl }}
            />
        </>
    );
};
