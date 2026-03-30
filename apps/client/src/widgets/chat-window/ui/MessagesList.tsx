import { ChatMessage } from "@/entities/chat-message";
import { useChatStore } from "@/store/useChatStore";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { FileIcon } from "@/shared/ui/phosphor";
import React, { useEffect } from "react";
import { FlatList, Text, View } from "react-native";
interface MessagesListProps {
    chatId: string;
    type: "chat" | "subject";
}
export const MessagesList = ({ chatId, type }: MessagesListProps) => {
    const { messages, isLoading, hasMore, loadMessages, loadMore } = useChatStore();
    useEffect(() => {
        loadMessages(chatId, type, true);
    }, [chatId, type]);
    const shouldShowAvatars = type === "subject";
    const showEmpty = !isLoading && messages.length === 0;
    return (<FlatList inverted={true} data={messages} keyExtractor={(item) => item.id} contentContainerStyle={{
            paddingHorizontal: 32,
            paddingVertical: 24,
            gap: 24,
            flexGrow: 1,
        }} renderItem={({ item }) => (<ChatMessage id={item.id} text={item.text} isOwn={item.isOwn} time={item.time} avatarUrl={item.avatarUrl} showAvatar={shouldShowAvatars}/>)} onEndReached={() => loadMore(chatId, type)} onEndReachedThreshold={0.2} ListEmptyComponent={showEmpty ? (<View className="flex-1 items-center justify-center py-20 px-6 min-h-[280px]">
            <View className="w-24 h-24 rounded-full bg-white/5 items-center justify-center mb-5">
              <FileIcon size={40} className="text-text-disabled" weight="regular"/>
            </View>
            <Text className="text-text-muted text-base font-medium text-center">
              Начните общение
            </Text>
          </View>) : null} ListFooterComponent={() => isLoading && hasMore ? (<View className="py-4">
            <ActivityIndicator className="text-brand-600"/>
          </View>) : null}/>);
};
