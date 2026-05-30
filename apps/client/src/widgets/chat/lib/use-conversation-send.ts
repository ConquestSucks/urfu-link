import { useCallback } from "react";
import type { DocumentPickerAsset } from "expo-document-picker";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import type { VoiceRecordingDraft } from "@/features/voice-message";
import { uploadChatAttachments } from "./upload-chat-attachments";

export const useConversationSend = (conversationId: string) => {
    const sendMessage = useChatStore((state) => state.sendMessage);

    return useCallback(
        async (
            text: string,
            files: DocumentPickerAsset[],
            replyToMessageId?: string,
            mentionUserIds?: string[],
            voiceDraft?: VoiceRecordingDraft | null,
        ) => {
            const assetIds = await uploadChatAttachments(files, voiceDraft);

            await sendMessage(
                conversationId,
                text,
                assetIds,
                replyToMessageId,
                mentionUserIds,
            );
        },
        [conversationId, sendMessage],
    );
};
