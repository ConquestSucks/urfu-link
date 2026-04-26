import React from "react";
import { Pressable, Text, View } from "react-native";
import { PushPinIcon, XIcon } from "@/shared/ui/phosphor";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import type { MessagesListHandle } from "./MessagesList";

interface PinnedBarProps {
    conversationId: string;
    listRef: React.RefObject<MessagesListHandle | null>;
}

export const PinnedBar = ({ conversationId, listRef }: PinnedBarProps) => {
    const conversation = useChatStore((s) =>
        s.conversations.find((c) => c.id === conversationId),
    );
    const messages = useChatStore((s) => s.messagesByConversation[conversationId]);
    const unpin = useChatStore((s) => s.unpinMessage);
    const setPendingScrollToMessageId = useChatStore((s) => s.setPendingScrollToMessageId);

    const pinnedIds = conversation?.pinnedMessageIds ?? [];
    if (pinnedIds.length === 0) return null;

    const lastId = pinnedIds[pinnedIds.length - 1];
    const lastPinned = messages?.find((m) => m.id === lastId);

    const handleJump = () => {
        if (!lastId) return;
        const ok = listRef.current?.scrollToMessage(lastId);
        if (!ok) setPendingScrollToMessageId(lastId);
    };

    const handleUnpin = async () => {
        if (!lastId) return;
        try {
            await unpin(conversationId, lastId);
        } catch (e) {
            console.error("Unpin failed", e);
        }
    };

    return (
        <Pressable
            onPress={handleJump}
            className="flex-row items-center gap-2 px-3 py-2 border-b border-white/5 bg-white/[0.02] active:bg-white/5"
        >
            <PushPinIcon size={14} className="text-brand-400" weight="fill" />
            <View className="flex-1 min-w-0">
                <Text className="text-brand-400 text-[11px] font-semibold">
                    Закреплено
                    {pinnedIds.length > 1 ? ` · ${pinnedIds.length}` : ""}
                </Text>
                <Text className="text-text-subtle text-xs" numberOfLines={1}>
                    {lastPinned?.body ?? "Загрузка…"}
                </Text>
            </View>
            <Pressable onPress={handleUnpin} hitSlop={6} className="p-1">
                <XIcon size={14} className="text-text-muted" />
            </Pressable>
        </Pressable>
    );
};
