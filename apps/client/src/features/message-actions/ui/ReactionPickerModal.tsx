import React, { lazy, Suspense } from "react";
import { View } from "react-native";
import { ModalOverlay } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";

const loadEmojiPicker = () => import("@/features/emoji-picker");
const EmojiPicker = lazy(() => loadEmojiPicker().then((module) => ({ default: module.EmojiPicker })));

const EmojiPickerFallback = () => (
    <View className="flex-1 items-center justify-center bg-app-card">
        <ActivityIndicator size="small" className="text-brand-400" />
    </View>
);

interface ReactionPickerModalProps {
    messageId: string | null;
    onClose: () => void;
}

export const ReactionPickerModal = ({ messageId, onClose }: ReactionPickerModalProps) => {
    const addReaction = useChatStore((s) => s.addReaction);

    React.useEffect(() => {
        if (messageId) void loadEmojiPicker();
    }, [messageId]);

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
            backdropClassName="px-4"
            contentClassName="bg-app-card border border-white/10 rounded-3xl overflow-hidden w-full max-w-[560px] h-[480px] max-h-[82vh]"
        >
            <View className="flex-1" style={{ minHeight: 0, minWidth: 0 }}>
                {messageId ? (
                    <Suspense fallback={<EmojiPickerFallback />}>
                        <EmojiPicker onPick={handlePick} />
                    </Suspense>
                ) : null}
            </View>
        </ModalOverlay>
    );
};
