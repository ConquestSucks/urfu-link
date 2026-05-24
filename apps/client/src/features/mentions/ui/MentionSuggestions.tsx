import React from "react";
import { FlatList, Pressable, Text, View } from "react-native";
import type { ConversationParticipantDto } from "@urfu-link/api-client";
import { Avatar } from "@/shared/ui";

interface MentionSuggestionsProps {
    items: ConversationParticipantDto[];
    onSelect: (item: ConversationParticipantDto) => void;
}

// Dropdown поверх ChatInput. Высота ограничена ~3 строками; FlatList с
// keyboardShouldPersistTaps="handled", чтобы тап по элементу не закрывал
// клавиатуру до того, как onSelect успеет отработать.
export const MentionSuggestions = ({ items, onSelect }: MentionSuggestionsProps) => {
    if (items.length === 0) return null;

    return (
        <View className="bg-app-card border border-white/10 rounded-2xl overflow-hidden mb-2 max-h-48">
            <FlatList
                data={items}
                keyExtractor={(p) => p.userId}
                keyboardShouldPersistTaps="handled"
                renderItem={({ item }) => (
                    <Pressable
                        onPress={() => onSelect(item)}
                        className="flex-row items-center gap-3 px-3 py-2 active:bg-white/5"
                    >
                        <Avatar size={28} src={item.avatarUrl || undefined} name={item.displayName} />
                        <View className="flex-1 min-w-0">
                            <Text
                                className="text-white text-sm font-medium"
                                numberOfLines={1}
                            >
                                {item.displayName || "Пользователь"}
                            </Text>
                        </View>
                    </Pressable>
                )}
            />
        </View>
    );
};
