import React from "react";
import { View } from "react-native";
import { ModalOverlay } from "@/shared/ui";
import { EmojiPicker } from "@/features/emoji-picker";
import { useChatStore } from "@/entities/conversation/model/chat-store";

interface ReactionPickerModalProps {
    messageId: string | null;
    onClose: () => void;
}

export const ReactionPickerModal = ({ messageId, onClose }: ReactionPickerModalProps) => {
    const addReaction = useChatStore((s) => s.addReaction);

    const handlePick = async (emoji: string) => {
        if (!messageId) return;
        try {
            await addReaction(messageId, emoji);
        } catch (e) {
            console.error("Failed to add reaction", e);
        }
        onClose();
    };

    return (
        <ModalOverlay
            visible={!!messageId}
            onClose={onClose}
            contentClassName="bg-app-card border border-white/10 rounded-3xl overflow-hidden w-[360px] h-[420px]"
        >
            <View className="flex-1">
                <EmojiPicker onPick={handlePick} />
            </View>
        </ModalOverlay>
    );
};
