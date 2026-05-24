import React from "react";
import { Pressable, Text, View } from "react-native";
import { SearchResultDto } from "@urfu-link/api-client";
import { Avatar } from "@/shared/ui";

interface SearchResultItemProps {
    item: SearchResultDto;
    onPress?: (item: SearchResultDto) => void;
    showConversationLabel?: boolean;
}

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

export const SearchResultItem = ({
    item,
    onPress,
    showConversationLabel = true,
}: SearchResultItemProps) => {
    const time = new Date(item.createdAtUtc).toLocaleString([], {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
    });

    const preview = item.conversationPreview;
    const authorName = preview?.senderName?.trim() || `Пользователь ${item.senderId.slice(0, 6)}`;
    const directTitle =
        preview?.type === "Direct" && preview.title?.trim() && preview.title.trim() !== authorName
            ? `Чат с ${preview.title.trim()}`
            : null;
    const groupTitle =
        preview?.type === "Group"
            ? preview.title?.trim() || "Групповой чат"
            : null;
    const conversationLabel = showConversationLabel ? directTitle ?? groupTitle : null;

    return (
        <Pressable
            onPress={() => onPress?.(item)}
            className="px-4 py-3 active:bg-white/5 flex-row gap-3"
        >
            <Avatar size={40} src={preview?.avatarUrl} name={authorName} />
            <View className="flex-1 min-w-0">
                <View className="flex-row justify-between items-start mb-1">
                    <Text
                        className="text-white text-sm font-semibold flex-1"
                        numberOfLines={1}
                    >
                        {authorName}
                    </Text>
                    <Text className="text-text-muted text-xs ml-2">{time}</Text>
                </View>
                {conversationLabel && (
                    <Text
                        className="text-text-subtle text-xs mb-0.5"
                        numberOfLines={1}
                    >
                        {conversationLabel}
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
