import React, { useEffect, useMemo } from "react";
import { FlatList, Pressable, Text, View } from "react-native";
import { ModalOverlay, Avatar, EmptyState } from "@/shared/ui";
import { ChatsCircleIcon } from "@/shared/ui/phosphor";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useParticipantsStore } from "@/entities/conversation/model/participants-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import type { ConversationParticipantDto, ConversationPreview } from "@urfu-link/api-client";

interface ForwardPickerModalProps {
    messageIds: string[] | null;
    onClose: () => void;
}

export const ForwardPickerModal = ({ messageIds, onClose }: ForwardPickerModalProps) => {
    const conversations = useChatStore((s) => s.conversations);
    const messagesByConversation = useChatStore((s) => s.messagesByConversation);
    const forwardMessages = useChatStore((s) => s.forwardMessages);
    const participantsByConversation = useParticipantsStore((s) => s.byConversationId);
    const loadParticipants = useParticipantsStore((s) => s.load);
    const currentUserId = useCurrentUserId();

    const visibleConversations = useMemo(
        () =>
            conversations.filter((conversation) => {
                if (conversation.type !== "Direct") return true;
                return (
                    conversation.lastMessagePreview != null ||
                    (messagesByConversation[conversation.id]?.length ?? 0) > 0
                );
            }),
        [conversations, messagesByConversation],
    );

    useEffect(() => {
        for (const conversation of visibleConversations) {
            if (conversation.type !== "Direct") continue;
            if (participantsByConversation[conversation.id]) continue;

            loadParticipants(conversation.id).catch(() => {
                /* fail-open: fallback title remains until participants can be loaded */
            });
        }
    }, [loadParticipants, participantsByConversation, visibleConversations]);

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
                data={visibleConversations}
                keyExtractor={(c) => c.id}
                renderItem={({ item }) => {
                    const peer = getDirectPeer(
                        item,
                        participantsByConversation[item.id]?.items,
                        currentUserId,
                    );
                    const name = resolveConversationName(item, peer);
                    const avatarUrl =
                        item.type === "Direct" && peer?.avatarUrl?.trim()
                            ? peer.avatarUrl
                            : "";
                    return (
                        <Pressable
                            testID={`forward-conversation-${item.id}`}
                            onPress={() => handlePick(item.id)}
                            className="flex-row items-center gap-3 px-4 py-3 transition-colors hover:bg-white/5 active:bg-white/5"
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

const getDirectPeer = (
    conversation: ConversationPreview,
    participants: ConversationParticipantDto[] | undefined,
    currentUserId: string | null,
) => {
    if (conversation.type !== "Direct") return null;

    return (
        participants?.find((participant) => participant.userId !== currentUserId) ??
        participants?.[0] ??
        null
    );
};

const resolveConversationName = (
    conversation: ConversationPreview,
    peer: ConversationParticipantDto | null,
) => {
    if (conversation.type !== "Direct") {
        return conversation.title ?? "Дисциплина";
    }

    return peer?.displayName?.trim() || conversation.title || "Личный чат";
};
