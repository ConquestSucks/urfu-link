import React from "react";
import { Pressable, Text, View } from "react-native";
import { SearchResultDto } from "@urfu-link/api-client";

interface SearchResultItemProps {
    item: SearchResultDto;
    onPress?: (item: SearchResultDto) => void;
}

/**
 * Renders highlighted text — wraps matched portions in accent color.
 * The server returns HTML-like <mark>word</mark> tags for highlights.
 */
const HighlightedText = ({ text }: { text: string }) => {
    // Simple parser: split on <mark>...</mark>
    const parts = text.split(/(<mark>.*?<\/mark>)/g);
    return (
        <Text className="text-text-subtle text-sm leading-5">
            {parts.map((part, i) => {
                if (part.startsWith("<mark>") && part.endsWith("</mark>")) {
                    const content = part.slice(6, -7);
                    return (
                        <Text key={i} className="text-brand-400 font-semibold">
                            {content}
                        </Text>
                    );
                }
                return <Text key={i}>{part}</Text>;
            })}
        </Text>
    );
};

export const SearchResultItem = ({ item, onPress }: SearchResultItemProps) => {
    const time = new Date(item.createdAt).toLocaleString([], {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
    });

    return (
        <Pressable
            onPress={() => onPress?.(item)}
            className="px-4 py-3 active:bg-white/5"
        >
            <View className="flex-row justify-between items-start mb-1">
                <Text className="text-text-muted text-xs">{time}</Text>
            </View>
            {item.highlightedBody ? (
                <HighlightedText text={item.highlightedBody} />
            ) : (
                <Text className="text-text-subtle text-sm" numberOfLines={2}>
                    {item.body}
                </Text>
            )}
        </Pressable>
    );
};
