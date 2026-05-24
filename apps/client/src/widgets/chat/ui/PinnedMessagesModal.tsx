import React, { useEffect } from "react";
import { Pressable, ScrollView, Text, View } from "react-native";
import type { MessageDto } from "@urfu-link/api-client";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { ModalOverlay } from "@/shared/ui";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { PushPinIcon, XIcon } from "@/shared/ui/phosphor";

interface PinnedMessagesModalProps {
    visible: boolean;
    conversationId: string;
    onClose: () => void;
    onJumpToMessage: (messageId: string) => void;
}

const EMPTY_PINNED_MESSAGES: MessageDto[] = [];

const formatPinnedTime = (value: string | undefined) => {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
};

const getPreview = (message: MessageDto) => {
    if (message.state === "Deleted") return "Сообщение удалено";
    if (message.body) return message.body;
    return message.attachments.length > 0 ? "Вложение" : "Без текста";
};

export const PinnedMessagesModal = ({
    visible,
    conversationId,
    onClose,
    onJumpToMessage,
}: PinnedMessagesModalProps) => {
    const pinned = useChatStore(
        (s) => s.pinnedMessagesByConversation[conversationId] ?? EMPTY_PINNED_MESSAGES,
    );
    const isLoading = useChatStore((s) => s.pinnedLoadingByConversation[conversationId] ?? false);
    const loadPinnedMessages = useChatStore((s) => s.loadPinnedMessages);
    const unpinMessage = useChatStore((s) => s.unpinMessage);

    useEffect(() => {
        if (!visible) return;
        void loadPinnedMessages(conversationId);
    }, [conversationId, loadPinnedMessages, visible]);

    return (
        <ModalOverlay
            visible={visible}
            onClose={onClose}
            contentClassName="bg-app-card border border-white/10 rounded-2xl overflow-hidden w-full max-w-[420px]"
            backdropClassName="px-4"
        >
            <View className="px-4 py-3 border-b border-white/5 flex-row items-center gap-2">
                <PushPinIcon size={18} className="text-brand-400" weight="fill" />
                <Text className="text-white text-base font-semibold">Закрепленные</Text>
            </View>

            {isLoading && pinned.length === 0 ? (
                <View className="py-8 items-center">
                    <ActivityIndicator className="text-brand-600" />
                </View>
            ) : pinned.length === 0 ? (
                <View className="px-4 py-8">
                    <Text className="text-center text-text-subtle text-sm">
                        Закрепленных сообщений пока нет
                    </Text>
                </View>
            ) : (
                <ScrollView className="max-h-[420px]" contentContainerStyle={{ paddingVertical: 6 }}>
                    {pinned.map((message) => (
                        <Pressable
                            key={message.id}
                            onPress={() => onJumpToMessage(message.id)}
                            className="mx-2 px-3 py-3 rounded-xl flex-row items-center gap-3 active:bg-white/5"
                        >
                            <View className="flex-1 min-w-0">
                                <Text className="text-white text-sm" numberOfLines={2}>
                                    {getPreview(message)}
                                </Text>
                                <Text className="text-text-muted text-[11px] mt-1">
                                    {formatPinnedTime(message.createdAt)}
                                </Text>
                            </View>
                            <Pressable
                                testID={`pinned-unpin-${message.id}`}
                                hitSlop={8}
                                onPress={(event) => {
                                    event?.stopPropagation?.();
                                    void unpinMessage(conversationId, message.id);
                                }}
                                className="p-1 rounded-lg active:bg-white/10"
                            >
                                <XIcon size={16} className="text-text-muted" />
                            </Pressable>
                        </Pressable>
                    ))}
                </ScrollView>
            )}
        </ModalOverlay>
    );
};
