import React from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { ChatHeader } from "./chat-header/ChatHeader";
import { ChatInput } from "./chat-input/Input";
import { MessagesList } from "./MessagesList";
import { SubjectHeader } from "./subject-header/SubjectHeader";

export const ChatView = () => {
    const { currentTab, params } = useInboxRouting();
    
    const chatId = params.id as string;
    const type = currentTab === "chats" ? "chat" : "subject";

    if (!chatId) return null; 

    return (
        <View className="bg-app-card flex-1">
            {type === "chat" ? (
                <ChatHeader chatId={chatId} />
            ) : (
                <SubjectHeader subjectId={chatId} />
            )}
            
            <MessagesList chatId={chatId} type={type} />
            <ChatInput onSend={() => {}} />
        </View>
    );
};