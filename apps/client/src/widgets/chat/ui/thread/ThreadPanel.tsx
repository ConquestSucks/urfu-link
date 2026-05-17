import React, { useCallback, useEffect, useMemo } from "react";
import { FlatList, Pressable, Text, TextInput, View } from "react-native";
import { ArrowBendUpLeftIcon, XIcon } from "@/shared/ui/phosphor";
import { EmptyState } from "@/shared/ui";
import { useThreadStore } from "@/entities/conversation/model/thread-store";
import { useChatStore, mapMessageToProps } from "@/entities/conversation/model/chat-store";
import { ChatMessage } from "@/entities/chat-message";
import { useCurrentUserId } from "@/shared/store/auth-store";
import type { MessageDto } from "@urfu-link/api-client";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";

interface ThreadPanelProps {
    rootMessageId: string;
    onClose: () => void;
}

export const ThreadPanel = ({ rootMessageId, onClose }: ThreadPanelProps) => {
    const messages = useThreadStore((s) => s.messagesByThread[rootMessageId] ?? []);
    const root = useThreadStore((s) => s.rootsById[rootMessageId]);
    const isLoading = useThreadStore((s) => !!s.loadingByThread[rootMessageId]);
    const hasMore = useThreadStore((s) => !!s.hasMoreByThread[rootMessageId]);
    const loadThread = useThreadStore((s) => s.loadThread);
    const loadMoreThread = useThreadStore((s) => s.loadMoreThread);
    const subscribeThread = useThreadStore((s) => s.subscribeThread);
    const unsubscribeThread = useThreadStore((s) => s.unsubscribeThread);
    const replyInThread = useThreadStore((s) => s.replyInThread);

    const conversationRoot = useChatStore((s) => {
        for (const list of Object.values(s.messagesByConversation)) {
            const found = list.find((m) => m.id === rootMessageId);
            if (found) return found;
        }
        return undefined;
    });

    const currentUserId = useCurrentUserId();
    const [body, setBody] = React.useState("");

    useEffect(() => {
        loadThread(rootMessageId, true);
        subscribeThread(rootMessageId);
        return () => {
            unsubscribeThread(rootMessageId);
        };
    }, [rootMessageId]);

    const rootMsg = root ?? conversationRoot;

    const handleSend = useCallback(async () => {
        const trimmed = body.trim();
        if (!trimmed) return;
        try {
            await replyInThread(rootMessageId, trimmed);
            setBody("");
        } catch (e) {
            console.error("Reply in thread failed", e);
        }
    }, [body, rootMessageId, replyInThread]);

    const renderItem = useMemo(
        () =>
            ({ item }: { item: MessageDto }) => {
                const view = mapMessageToProps(item, currentUserId);
                return (
                    <View className="px-4">
                        <ChatMessage
                            id={view.id}
                            text={view.text}
                            isOwn={view.isOwn}
                            seen={view.seen}
                            time={view.time}
                            avatarUrl={view.avatarUrl}
                            attachments={view.attachments}
                            replyTo={item.replyTo ?? null}
                            reactions={item.reactions}
                            editedAtUtc={item.editedAtUtc}
                            forwardedFrom={item.forwardedFrom ?? null}
                            isDeleted={item.state === "Deleted"}
                            threadReplyCount={0}
                        />
                    </View>
                );
            },
        [currentUserId],
    );

    return (
        <View className="flex-1 bg-app-card border-l border-white/5">
            <View className="flex-row items-center justify-between px-4 py-3 border-b border-white/5">
                <View>
                    <Text className="text-white text-base font-semibold">Тред</Text>
                    {rootMsg && (
                        <Text className="text-text-muted text-xs" numberOfLines={1}>
                            {rootMsg.body || "вложение"}
                        </Text>
                    )}
                </View>
                <Pressable onPress={onClose} hitSlop={8} className="p-1">
                    <XIcon size={20} className="text-text-subtle" />
                </Pressable>
            </View>

            <FlatList
                className="flex-1 py-4"
                inverted
                data={messages}
                keyExtractor={(m) => m.id}
                renderItem={renderItem}
                contentContainerStyle={{ gap: 16 }}
                onEndReached={() => {
                    if (hasMore) loadMoreThread(rootMessageId);
                }}
                onEndReachedThreshold={0.3}
                ListEmptyComponent={
                    isLoading ? (
                        <View className="py-8 items-center">
                            <ActivityIndicator className="text-brand-600" />
                        </View>
                    ) : (
                        <EmptyState
                            size="compact"
                            icon={ArrowBendUpLeftIcon}
                            title="Пока нет ответов"
                        />
                    )
                }
            />

            <View className="border-t border-white/5 p-3 flex-row items-end gap-2">
                <View className="flex-1 bg-white/5 rounded-2xl px-3 py-2">
                    <TextInput
                        value={body}
                        onChangeText={setBody}
                        placeholder="Ответить в треде…"
                        placeholderTextColor="#8B8FA8"
                        multiline
                        className="text-white text-[15px] min-h-[24px]"
                        style={{ textAlignVertical: "center" }}
                    />
                </View>
                <Pressable
                    onPress={handleSend}
                    disabled={body.trim().length === 0}
                    className={`px-4 py-2 rounded-xl ${body.trim().length === 0 ? "bg-brand-600/30" : "bg-brand-600 active:opacity-80"}`}
                >
                    <Text className="text-white font-medium">Отправить</Text>
                </Pressable>
            </View>
        </View>
    );
};
