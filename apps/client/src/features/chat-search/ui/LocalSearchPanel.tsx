import React, { useRef } from "react";
import {
    View,
    TextInput,
    Pressable,
    FlatList,
    Text,
    ActivityIndicator as RNActivityIndicator,
} from "react-native";
import { MagnifyingGlassIcon, XIcon } from "@/shared/ui/phosphor";
import { EmptyState } from "@/shared/ui";
import { SearchResultDto } from "@urfu-link/api-client";
import { useLocalSearch } from "../model/use-search";
import { SearchResultItem } from "./SearchResultItem";

interface LocalSearchPanelProps {
    conversationId: string;
    onResultPress: (item: SearchResultDto) => void;
    onClose: () => void;
}

export const LocalSearchPanel = ({ conversationId, onResultPress, onClose }: LocalSearchPanelProps) => {
    const { query, results, isLoading, hasMore, onQueryChange, loadMore, clear } = useLocalSearch(conversationId);
    const inputRef = useRef<TextInput>(null);

    const handleClose = () => {
        clear();
        onClose();
    };

    return (
        <View className="border-b border-white/5 bg-app-card">
            {/* Search bar */}
            <View className="flex-row items-center px-3 py-2 gap-2">
                <MagnifyingGlassIcon size={18} className="text-text-muted" />
                <TextInput
                    ref={inputRef}
                    autoFocus
                    className="flex-1 text-white text-[15px]"
                    placeholder="Поиск по переписке..."
                    placeholderTextColor="#8B8FA8"
                    value={query}
                    onChangeText={onQueryChange}
                    returnKeyType="search"
                />
                {query.length > 0 && (
                    <Pressable onPress={() => onQueryChange("")} hitSlop={8}>
                        <XIcon size={18} className="text-text-muted" />
                    </Pressable>
                )}
                <Pressable onPress={handleClose} hitSlop={8}>
                    <Text className="text-brand-400 text-sm font-medium">Отмена</Text>
                </Pressable>
            </View>

            {/* Results */}
            {query.length >= 2 && (
                <View className="max-h-64 border-t border-white/5">
                    {isLoading && results.length === 0 ? (
                        <View className="py-6 items-center">
                            <RNActivityIndicator color="#6B6FFF" />
                        </View>
                    ) : results.length === 0 ? (
                        <EmptyState
                            size="compact"
                            icon={MagnifyingGlassIcon}
                            title="Ничего не найдено"
                        />
                    ) : (
                        <FlatList
                            data={results}
                            keyExtractor={(item) => item.messageId}
                            renderItem={({ item }) => (
                                <SearchResultItem item={item} onPress={onResultPress} />
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
                    )}
                </View>
            )}
        </View>
    );
};
