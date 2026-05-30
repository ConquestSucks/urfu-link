import { useCallback } from "react";
import type { DocumentPickerAsset } from "expo-document-picker";
import { apiClient } from "@/shared/lib/api";
import { useChatStore } from "@/entities/conversation/model/chat-store";

export const useConversationSend = (conversationId: string) => {
    const sendMessage = useChatStore((state) => state.sendMessage);

    return useCallback(
        async (
            text: string,
            files: DocumentPickerAsset[],
            replyToMessageId?: string,
            mentionUserIds?: string[],
        ) => {
            const assetIds: string[] = [];

            for (const file of files) {
                const initRes = await apiClient.media.initUpload({
                    fileName: file.name,
                    size: file.size ?? 0,
                    mimeType: file.mimeType ?? "application/octet-stream",
                    visibility: "Private",
                });

                const uploadBody = file.file ?? (await fetch(file.uri).then((r) => r.blob()));
                await fetch(initRes.presignedPutUrl, {
                    method: "PUT",
                    body: uploadBody,
                });

                await apiClient.media.completeUpload({ assetId: initRes.assetId });
                assetIds.push(initRes.assetId);
            }

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
