import React from "react";
import { View } from "react-native";
import { ChatHeader } from "./chat-header/ChatHeader";
import { ChatInput } from "./Input";
import { MessagesList } from "./MessagesList";
import { SubjectHeader } from "./subject-header/SubjectHeader";

interface ChatViewProps {
  chatId: string;
  type: "chat" | "subject";
}

export const ChatView = ({ chatId, type }: ChatViewProps) => {
  return (
    <View className="bg-[#0B1225] flex-1">
      {type === "chat" ? (
        <ChatHeader chatId={chatId} />
      ) : (
        <SubjectHeader subjectId={chatId} />
      )}
      <MessagesList chatId={chatId} type={type} />
      <ChatInput />
    </View>
  );
};
