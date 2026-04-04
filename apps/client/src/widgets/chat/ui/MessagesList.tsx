import { ChatMessage } from "@/entities/chat-message";
import { useChatStore } from "@/shared/store/useChatStore";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { FileIcon } from "@/shared/ui/phosphor";
import React, { useEffect } from "react";
import { FlatList, Text, View } from "react-native";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { ChatMessageSkeleton } from "@/entities/chat-message/ui/ChatMessageSkeleton";

interface MessagesListProps {
    chatId: string;
    type: "chat" | "subject";
}

export const MessagesList = ({ chatId, type }: MessagesListProps) => {
    const { messages, isLoading, hasMore, loadMessages, loadMore } = useChatStore();
    const { isMobile } = useWindowSize();

    useEffect(() => {
        loadMessages(chatId, type, true);
    }, [chatId, type]);

    const shouldShowAvatars = type === "subject";
    
    const isInitialLoading = isLoading && messages.length === 0;

    if (isInitialLoading) {
        return (
            <View className="flex-1 px-6 py-6 justify-end overflow-hidden" style={{ gap: 24 }}>
                <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                <ChatMessageSkeleton isOwn={true} />
                <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                <ChatMessageSkeleton isOwn={true} />
            </View>
        );
    }

    return (
        <FlatList
            className="flex-1 px-6 py-6"
            inverted={true}
            data={messages}
            keyExtractor={(item) => item.id}
            contentContainerStyle={{
                gap: 24,
                flexGrow: 1,
            }}
            renderItem={({ item }) => (
                <ChatMessage
                    id={item.id}
                    text={item.text}
                    isOwn={item.isOwn}
                    seen={item.seen}
                    time={item.time}
                    avatarUrl={item.avatarUrl}
                    showAvatar={shouldShowAvatars}
                />
            )}
            onEndReached={() => loadMore(chatId, type)}
            onEndReachedThreshold={0.2}
            ListEmptyComponent={
                <View className="flex-1 items-center justify-center py-20 px-6 min-h-[280px]">
                    <View className="w-24 h-24 rounded-full bg-white/5 items-center justify-center mb-5">
                        <FileIcon size={40} className="text-text-disabled" weight="regular" />
                    </View>
                    <Text className="text-text-muted text-base font-medium text-center">
                        Начните общение
                    </Text>
                </View>
            }
            ListFooterComponent={() =>
                isLoading && hasMore ? (
                    <View className="py-4">
                        <ActivityIndicator className="text-brand-600" />
                    </View>
                ) : null
            }
        />
    );
};