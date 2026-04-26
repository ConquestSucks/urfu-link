import React from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { ChatHeader } from "./chat-header/ChatHeader";
import { ChatInput } from "./chat-input/Input";
import { MessagesList } from "./MessagesList";
import { SubjectHeader } from "./subject-header/SubjectHeader";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { apiClient } from "@/shared/lib/api";
import type { DocumentPickerAsset } from "expo-document-picker";

export const ChatView = () => {
    const { currentTab, params } = useInboxRouting();
    const { sendMessage } = useChatStore();
    
    const chatId = params.id as string;
    const type = currentTab === "chats" ? "chat" : "subject";

    if (!chatId) return null;

    const handleSend = async (text: string, files: DocumentPickerAsset[]) => {
        try {
            const assetIds: string[] = [];
            
            // Temporary simple upload:
            for (const file of files) {
                const initRes = await apiClient.media.initUpload({
                    fileName: file.name,
                    size: file.size ?? 0,
                    mimeType: file.mimeType ?? "application/octet-stream",
                    visibility: "Private"
                });
                
                // Need to PUT the file to initRes.presignedPutUrl
                const blob = await fetch(file.uri).then(r => r.blob());
                await fetch(initRes.presignedPutUrl, {
                    method: "PUT",
                    body: blob
                });
                
                await apiClient.media.completeUpload({ assetId: initRes.assetId });
                assetIds.push(initRes.assetId);
            }
            
            await sendMessage(chatId, text, assetIds);
        } catch (error) {
            console.error("Failed to send message", error);
        }
    };

    return (
        <View className="bg-app-card flex-1">
            {type === "chat" ? (
                <ChatHeader chatId={chatId} />
            ) : (
                <SubjectHeader subjectId={chatId} />
            )}
            
            <MessagesList chatId={chatId} type={type} />
            <ChatInput onSend={handleSend} />
        </View>
    );
};