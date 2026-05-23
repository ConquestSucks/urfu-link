import React from "react";
import { Pressable, Text, View } from "react-native";
import { GlobalSearchScope } from "../model/search-store";

interface SearchScopeTabsProps {
    value: GlobalSearchScope;
    onChange: (next: GlobalSearchScope) => void;
}

const TABS: { id: GlobalSearchScope; label: string }[] = [
    { id: "messages", label: "Сообщения" },
    { id: "users", label: "Люди" },
];

export const SearchScopeTabs = ({ value, onChange }: SearchScopeTabsProps) => {
    return (
        <View className="flex-row px-4 pt-2 pb-3 gap-2">
            {TABS.map((tab) => {
                const active = tab.id === value;
                return (
                    <Pressable
                        key={tab.id}
                        onPress={() => onChange(tab.id)}
                        className={[
                            "px-3 py-1.5 rounded-full",
                            active ? "bg-brand-500" : "bg-white/5 active:bg-white/10",
                        ].join(" ")}
                    >
                        <Text
                            className={[
                                "text-xs font-semibold",
                                active ? "text-white" : "text-text-subtle",
                            ].join(" ")}
                        >
                            {tab.label}
                        </Text>
                    </Pressable>
                );
            })}
        </View>
    );
};
