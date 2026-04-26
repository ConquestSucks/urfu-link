import React from "react";
import {
    View,
    FlatList,
    Text,
    ActivityIndicator as RNActivityIndicator,
} from "react-native";
import { SearchResultDto } from "@urfu-link/api-client";
import { useGlobalSearch } from "../model/use-search";
import { SearchResultItem } from "./SearchResultItem";
import { useRouter } from "expo-router";
import { useChatStore } from "@/entities/conversation/model/chat-store";

interface GlobalSearchPanelProps {
    /** Called when user picks a result. Default: navigate to conversation. */
    onResultPress?: (item: SearchResultDto) => void;
}

export const GlobalSearchPanel = ({ onResultPress }: GlobalSearchPanelProps) => {
    const { query, results, isLoading, hasMore, loadMore } = useGlobalSearch();
    const router = useRouter();
    const setPendingScrollToMessageId = useChatStore((s) => s.setPendingScrollToMessageId);

    const handlePress = (item: SearchResultDto) => {
        if (onResultPress) {
            onResultPress(item);
            return;
        }
        setPendingScrollToMessageId(item.messageId);
        router.push(`/chats/${item.conversationId}` as never);
    };

    if (query.length < 2) return null;

    if (isLoading && results.length === 0) {
        return (
            <View className="flex-1 items-center justify-center py-8">
                <RNActivityIndicator color="#6B6FFF" />
            </View>
        );
    }

    if (results.length === 0) {
        return (
            <View className="flex-1 items-center justify-center py-8">
                <Text className="text-text-muted text-sm">Ничего не найдено</Text>
            </View>
        );
    }

    return (
        <FlatList
            className="flex-1"
            data={results}
            keyExtractor={(item) => item.messageId}
            renderItem={({ item }) => (
                <SearchResultItem item={item} onPress={handlePress} />
            )}
            onEndReached={hasMore ? loadMore : undefined}
            onEndReachedThreshold={0.3}
            keyboardShouldPersistTaps="handled"
            ListFooterComponent={
                isLoading && hasMore ? (
                    <View className="py-3 items-center">
                        <RNActivityIndicator color="#6B6FFF" />
                    </View>
                ) : null
            }
        />
    );
};
