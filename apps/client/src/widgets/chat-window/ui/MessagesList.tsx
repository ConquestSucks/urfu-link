import { ChatMessage } from "@/entities/chat-message";
import { useChatStore } from "@/store/useChatStore";
import React, { useEffect } from "react";
import { ActivityIndicator, FlatList, View } from "react-native";

interface MessagesListProps {
  chatId: string;
  type: "chat" | "subject";
}

export const MessagesList = ({ chatId, type }: MessagesListProps) => {
  const { messages, isLoading, hasMore, loadMessages, loadMore } =
    useChatStore();

  useEffect(() => {
    loadMessages(chatId, type, true);
  }, [chatId, type]);

  const shouldShowAvatars = type === "subject";

  return (
    <FlatList
      inverted={true}
      data={messages}
      keyExtractor={(item) => item.id}
      contentContainerStyle={{
        paddingHorizontal: 32,
        paddingVertical: 24,
        gap: 24,
        flexGrow: 1,
      }}
      renderItem={({ item }) => (
        <ChatMessage
          id={item.id}
          text={item.text}
          isOwn={item.isOwn}
          time={item.time}
          avatarUrl={item.avatarUrl}
          showAvatar={shouldShowAvatars}
        />
      )}
      onEndReached={() => loadMore(chatId, type)}
      onEndReachedThreshold={0.2}
      ListFooterComponent={() =>
        isLoading && hasMore ? (
          <View className="py-4">
            <ActivityIndicator color="#2B7FFF" />
          </View>
        ) : null
      }
    />
  );
};
