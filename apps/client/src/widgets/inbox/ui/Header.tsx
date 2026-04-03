import React from "react";
import { Text, View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { ViewToggle } from "@/entities/view";
import { SearchBar } from "@/shared/ui";

export const Header = ({ title }: { title: string }) => {
    const { currentView, createViewHref } = useInboxRouting();

    return (
        <View className="px-6">
            <View className="py-5 flex-row items-center justify-between">
                <Text numberOfLines={1} className="text-xl text-white font-bold">
                    {title}
                </Text>
                <ViewToggle currentView={currentView} createHref={createViewHref} />
            </View>
            <SearchBar />
        </View>
    );
};
