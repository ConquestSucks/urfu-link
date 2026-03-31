import { ViewToggle, ViewType } from "@/entities/view";
import { SearchBar } from "@/shared/ui";
import { BellIcon, ChatCircleTextIcon } from "@/shared/ui/phosphor";
import React from "react";
import { Pressable, Text, View } from "react-native";
interface HeaderProps {
    title: string;
    currentView: ViewType;
    onCurrentViewChange: (view: ViewType) => void;
}
export const Header = ({ title, currentView, onCurrentViewChange }: HeaderProps) => {
    return (
        <View className="px-6">
            <View className="py-5 flex-row items-center justify-between">
                <Text numberOfLines={1} className="text-xl text-white font-bold">
                    {title}
                </Text>

                <ViewToggle currentView={currentView} onCurrentViewChange={onCurrentViewChange} />
            </View>
            <SearchBar />
        </View>
    );
};
