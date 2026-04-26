import React from "react";
import {
    View,
    TextInput,
    FlatList,
    Text,
    Pressable,
    ActivityIndicator as RNActivityIndicator,
} from "react-native";
import { MagnifyingGlassIcon } from "@/shared/ui/phosphor";
import { SearchResultDto } from "@urfu-link/api-client";
import { useGlobalSearch } from "../model/use-search";
import { SearchResultItem } from "./SearchResultItem";
import { useRouter } from "expo-router";

interface GlobalSearchPanelProps {
    /** Called when user picks a result — navigate to the conversation */
    onResultPress?: (item: SearchResultDto) => void;
}

export const GlobalSearchPanel = ({ onResultPress }: GlobalSearchPanelProps) => {
    const { query, results, isLoading, hasMore, onQueryChange, loadMore } = useGlobalSearch();
    const router = useRouter();

    const handlePress = (item: SearchResultDto) => {
        if (onResultPress) {
            onResultPress(item);
        } else {
            router.push(`/chats/${item.conversationId}`);
        }
    };

    return (
        <View className="flex-1">
            {/* Search input */}
            <View className="flex-row items-center bg-white/5 rounded-xl mx-4 mb-3 px-3 py-2 gap-2">
                <MagnifyingGlassIcon size={18} className="text-text-muted" />
                <TextInput
                    className="flex-1 text-white text-[15px]"
                    placeholder="Поиск по всем чатам..."
                    placeholderTextColor="#8B8FA8"
                    value={query}
                    onChangeText={onQueryChange}
                    returnKeyType="search"
                />
            </View>

            {/* Results */}
            {query.length >= 2 ? (
                isLoading && results.length === 0 ? (
                    <View className="flex-1 items-center justify-center py-8">
                        <RNActivityIndicator color="#6B6FFF" />
                    </View>
                ) : results.length === 0 ? (
                    <View className="flex-1 items-center justify-center py-8">
                        <Text className="text-text-muted text-sm">Ничего не найдено</Text>
                    </View>
                ) : (
                    <FlatList
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
                )
            ) : null}
        </View>
    );
};
