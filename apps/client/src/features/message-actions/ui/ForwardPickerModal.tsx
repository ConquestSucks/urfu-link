import React from "react";
import { FlatList, Pressable, Text, View } from "react-native";
import { ModalOverlay, Avatar, EmptyState } from "@/shared/ui";
import { ChatsCircleIcon } from "@/shared/ui/phosphor";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useInboxStore } from "@/shared/store/useInboxStore";

interface ForwardPickerModalProps {
    messageIds: string[] | null;
    onClose: () => void;
}

export const ForwardPickerModal = ({ messageIds, onClose }: ForwardPickerModalProps) => {
    const conversations = useChatStore((s) => s.conversations);
    const forwardMessages = useChatStore((s) => s.forwardMessages);
    const getChatById = useInboxStore((s) => s.getChatById);

    if (!messageIds || messageIds.length === 0) return null;

    const handlePick = async (conversationId: string) => {
        try {
            await forwardMessages(conversationId, messageIds);
        } catch (e) {
            console.error("Forward failed", e);
        }
        onClose();
    };

    return (
        <ModalOverlay
            visible={messageIds.length > 0}
            onClose={onClose}
            contentClassName="bg-app-card border border-white/10 rounded-2xl w-[420px] max-w-[90%] max-h-[70vh] overflow-hidden"
        >
            <View className="px-4 py-3 border-b border-white/5">
                <Text className="text-white text-base font-semibold">
                    Переслать в…
                </Text>
            </View>
            <FlatList
                data={conversations}
                keyExtractor={(c) => c.id}
                renderItem={({ item }) => {
                    const meta = getChatById(item.id);
                    const name =
                        meta?.name ??
                        item.title ??
                        (item.type === "Direct" ? "Личный чат" : "Дисциплина");
                    const avatarUrl = meta?.avatarUrl ?? "";
                    return (
                        <Pressable
                            onPress={() => handlePick(item.id)}
                            className="flex-row items-center gap-3 px-4 py-3 active:bg-white/5"
                        >
                            <Avatar size={36} src={avatarUrl} name={name} />
                            <Text
                                className="text-white text-[15px] flex-1"
                                numberOfLines={1}
                            >
                                {name}
                            </Text>
                        </Pressable>
                    );
                }}
                ListEmptyComponent={
                    <EmptyState
                        size="compact"
                        icon={ChatsCircleIcon}
                        title="Нет доступных чатов"
                    />
                }
            />
        </ModalOverlay>
    );
};
