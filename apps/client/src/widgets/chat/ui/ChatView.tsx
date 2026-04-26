import React, { useCallback, useRef, useState } from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { ChatHeader } from "./chat-header/ChatHeader";
import { ChatInput } from "./chat-input/Input";
import { MessagesList, type MessagesListHandle } from "./MessagesList";
import { SubjectHeader } from "./subject-header/SubjectHeader";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { apiClient } from "@/shared/lib/api";
import type { DocumentPickerAsset } from "expo-document-picker";
import { LocalSearchPanel } from "@/features/chat-search";
import type { SearchResultDto } from "@urfu-link/api-client";

export const ChatView = () => {
    const { currentTab, params } = useInboxRouting();
    const { sendMessage, setPendingScrollToMessageId } = useChatStore();
    const [isSearching, setIsSearching] = useState(false);
    const listRef = useRef<MessagesListHandle>(null);

    const chatId = params.id as string;
    const type = currentTab === "chats" ? "chat" : "subject";

    const handleSend = useCallback(
        async (text: string, files: DocumentPickerAsset[]) => {
            try {
                const assetIds: string[] = [];

                for (const file of files) {
                    const initRes = await apiClient.media.initUpload({
                        fileName: file.name,
                        size: file.size ?? 0,
                        mimeType: file.mimeType ?? "application/octet-stream",
                        visibility: "Private",
                    });

                    const blob = await fetch(file.uri).then((r) => r.blob());
                    await fetch(initRes.presignedPutUrl, {
                        method: "PUT",
                        body: blob,
                    });

                    await apiClient.media.completeUpload({ assetId: initRes.assetId });
                    assetIds.push(initRes.assetId);
                }

                await sendMessage(chatId, text, assetIds);
            } catch (error) {
                console.error("Failed to send message", error);
            }
        },
        [chatId, sendMessage],
    );

    const handleSearchResultPress = useCallback(
        (item: SearchResultDto) => {
            const scrolled = listRef.current?.scrollToMessage(item.messageId) ?? false;
            if (!scrolled) {
                setPendingScrollToMessageId(item.messageId);
            }
            setIsSearching(false);
        },
        [setPendingScrollToMessageId],
    );

    if (!chatId) return null;

    return (
        <View className="bg-app-card flex-1">
            {type === "chat" ? (
                isSearching ? (
                    <LocalSearchPanel
                        conversationId={chatId}
                        onResultPress={handleSearchResultPress}
                        onClose={() => setIsSearching(false)}
                    />
                ) : (
                    <ChatHeader chatId={chatId} onOpenSearch={() => setIsSearching(true)} />
                )
            ) : (
                <SubjectHeader subjectId={chatId} />
            )}

            <MessagesList ref={listRef} chatId={chatId} type={type} />
            <ChatInput conversationId={chatId} onSend={handleSend} />
        </View>
    );
};
