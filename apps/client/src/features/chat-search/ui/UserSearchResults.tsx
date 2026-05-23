import React from "react";
import { View, FlatList, ActivityIndicator as RNActivityIndicator } from "react-native";
import { SearchUserDto } from "@urfu-link/api-client";
import { Button, EmptyState } from "@/shared/ui";
import { MagnifyingGlassIcon, WarningCircleIcon } from "@/shared/ui/phosphor";
import { useUserSearch } from "../model/use-search";
import { useSearchStore } from "../model/search-store";
import { UserSearchResultItem } from "./UserSearchResultItem";
import { useRouter } from "expo-router";

export const UserSearchResults = () => {
    const { query, results, isLoading, error, hasMore, pendingUserId, loadMore, retry, openDirectWithUser } =
        useUserSearch();
    const clearGlobal = useSearchStore((s) => s.clearGlobal);
    const router = useRouter();

    const handlePress = async (item: SearchUserDto) => {
        const conversationId = await openDirectWithUser(item.id);
        if (!conversationId) return;
        // Сбрасываем поиск — иначе при возврате на Inbox пользователь увидит
        // прежнюю выдачу. Кроме того clearGlobal вернёт scope в "messages".
        clearGlobal();
        router.push(`/chats/${conversationId}` as never);
    };

    if (query.length < 2) return null;

    if (isLoading && results.length === 0) {
        return (
            <View className="flex-1 items-center justify-center py-8">
                <RNActivityIndicator color="#6B6FFF" />
            </View>
        );
    }

    if (error && results.length === 0) {
        return (
            <EmptyState
                size="full"
                icon={WarningCircleIcon}
                title="Не удалось загрузить результаты"
                description={error}
                action={<Button label="Повторить" onPress={retry} />}
            />
        );
    }

    if (results.length === 0) {
        return (
            <EmptyState
                size="full"
                icon={MagnifyingGlassIcon}
                title="Никого не нашли"
                description="Попробуйте другое имя или логин"
            />
        );
    }

    return (
        <FlatList
            className="flex-1"
            data={results}
            keyExtractor={(item) => item.id}
            renderItem={({ item }) => (
                <UserSearchResultItem
                    item={item}
                    isPending={pendingUserId === item.id}
                    onPress={handlePress}
                />
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
