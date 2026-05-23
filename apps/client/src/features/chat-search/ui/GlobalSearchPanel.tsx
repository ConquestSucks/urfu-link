import React from "react";
import {
    View,
    FlatList,
    ActivityIndicator as RNActivityIndicator,
} from "react-native";
import { SearchResultDto } from "@urfu-link/api-client";
import { Button, EmptyState } from "@/shared/ui";
import { MagnifyingGlassIcon, WarningCircleIcon } from "@/shared/ui/phosphor";
import { useGlobalSearch } from "../model/use-search";
import { useSearchStore } from "../model/search-store";
import { SearchResultItem } from "./SearchResultItem";
import { SearchFiltersBar } from "./SearchFiltersBar";
import { SearchScopeTabs } from "./SearchScopeTabs";
import { UserSearchResults } from "./UserSearchResults";
import { useRouter } from "expo-router";
import { useChatStore } from "@/entities/conversation/model/chat-store";

interface GlobalSearchPanelProps {
    /** Called when user picks a result. Default: navigate to conversation. */
    onResultPress?: (item: SearchResultDto) => void;
}

export const GlobalSearchPanel = ({ onResultPress }: GlobalSearchPanelProps) => {
    const { query, results, isLoading, error, hasMore, filters, onFiltersChange, loadMore, retry } =
        useGlobalSearch();
    const globalScope = useSearchStore((s) => s.globalScope);
    const setGlobalScope = useSearchStore((s) => s.setGlobalScope);
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

    const scopeTabs = <SearchScopeTabs value={globalScope} onChange={setGlobalScope} />;

    // На "Люди" фильтры (даты/вложения) не применимы — UserSearchResults рендерит
    // только результат поиска по людям. Табы остаются всегда, чтобы можно было
    // переключиться обратно на сообщения.
    if (globalScope === "users") {
        return (
            <View className="flex-1">
                {scopeTabs}
                <UserSearchResults />
            </View>
        );
    }

    // Bar над выдачей — даём управление фильтрами hasAttachments / attachmentType / date.
    // sender-picker для глобального поиска не показываем (нет конкретного списка
    // участников — нужно было бы дёргать всех знакомых юзеров).
    const filtersBar = <SearchFiltersBar value={filters} onChange={onFiltersChange} />;

    if (isLoading && results.length === 0) {
        return (
            <View className="flex-1">
                {scopeTabs}
                {filtersBar}
                <View className="flex-1 items-center justify-center py-8">
                    <RNActivityIndicator color="#6B6FFF" />
                </View>
            </View>
        );
    }

    if (error && results.length === 0) {
        return (
            <View className="flex-1">
                {scopeTabs}
                {filtersBar}
                <EmptyState
                    size="full"
                    icon={WarningCircleIcon}
                    title="Не удалось загрузить результаты"
                    description={error}
                    action={<Button label="Повторить" onPress={retry} />}
                />
            </View>
        );
    }

    if (results.length === 0) {
        return (
            <View className="flex-1">
                {scopeTabs}
                {filtersBar}
                <EmptyState
                    size="full"
                    icon={MagnifyingGlassIcon}
                    title="Ничего не найдено"
                    description="Попробуйте изменить запрос или фильтры"
                />
            </View>
        );
    }

    return (
        <View className="flex-1">
            {scopeTabs}
            {filtersBar}
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
        </View>
    );
};
