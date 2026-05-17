import React from "react";
import { Pressable, Text, View } from "react-native";
import { SearchResultDto } from "@urfu-link/api-client";
import { Avatar } from "@/shared/ui";

interface SearchResultItemProps {
    item: SearchResultDto;
    onPress?: (item: SearchResultDto) => void;
}

/**
 * Renders highlighted text — wraps matched portions in accent color.
 * The server returns HTML-like <mark>word</mark> tags for highlights.
 */
const HighlightedText = ({ text }: { text: string }) => {
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
    const time = new Date(item.createdAtUtc).toLocaleString([], {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
    });

    const preview = item.conversationPreview;
    const previewTitle =
        preview?.title ??
        (preview?.type === "Direct" ? "Личный чат" : null);
    const senderName = preview?.senderName;

    return (
        <Pressable
            onPress={() => onPress?.(item)}
            className="px-4 py-3 active:bg-white/5 flex-row gap-3"
        >
            <Avatar size={40} src={preview?.avatarUrl} name={previewTitle ?? "?"} />
            <View className="flex-1 min-w-0">
                <View className="flex-row justify-between items-start mb-1">
                    {previewTitle ? (
                        <Text
                            className="text-white text-sm font-semibold flex-1"
                            numberOfLines={1}
                        >
                            {previewTitle}
                        </Text>
                    ) : (
                        <View className="flex-1" />
                    )}
                    <Text className="text-text-muted text-xs ml-2">{time}</Text>
                </View>
                {senderName && (
                    <Text
                        className="text-text-subtle text-xs mb-0.5"
                        numberOfLines={1}
                    >
                        {senderName}
                    </Text>
                )}
                {item.highlightedSnippet ? (
                    <HighlightedText text={item.highlightedSnippet} />
                ) : (
                    <Text className="text-text-subtle text-sm" numberOfLines={2}>
                        {item.body}
                    </Text>
                )}
            </View>
        </Pressable>
    );
};
