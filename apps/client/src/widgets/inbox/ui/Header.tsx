import React from "react";
import { Text, View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { ViewToggle } from "@/entities/view";
import { SearchBar } from "@/shared/ui";
import { useSearchStore } from "@/features/chat-search";
import { useGlobalSearch } from "@/features/chat-search";

export const Header = ({ title }: { title: string }) => {
    const { currentView, createViewHref } = useInboxRouting();
    const globalQuery = useSearchStore((s) => s.globalQuery);
    const { onQueryChange } = useGlobalSearch();
    const searchPlaceholder =
        currentView === "notifications"
            ? "Поиск по уведомлениям..."
            : "Поиск по чатам, сообщениям и людям...";

    return (
        <View className="px-6">
            <View className="py-5 flex-row items-center justify-between">
                <Text numberOfLines={1} className="text-xl text-white font-bold">
                    {title}
                </Text>
                <ViewToggle currentView={currentView} createHref={createViewHref} />
            </View>
            <SearchBar
                value={globalQuery}
                onChange={onQueryChange}
                placeholder={searchPlaceholder}
            />
        </View>
    );
};
