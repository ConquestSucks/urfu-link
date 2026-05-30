import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { FlatList, Pressable, StyleSheet, Text, View } from "react-native";
import { ArrowBendUpLeftIcon, XIcon } from "@/shared/ui/phosphor";
import { EmptyState } from "@/shared/ui";
import { useThreadStore, useThreadMessages } from "@/entities/conversation/model/thread-store";
import { useChatStore, mapMessageToProps } from "@/entities/conversation/model/chat-store";
import { ChatMessage } from "@/entities/chat-message";
import { useCurrentUserId } from "@/shared/store/auth-store";
import type { MessageDto } from "@urfu-link/api-client";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { ChatInput } from "../chat-input/Input";
import type { DocumentPickerAsset } from "expo-document-picker";
import type { VoiceRecordingDraft } from "@/features/voice-message";
import { uploadChatAttachments } from "../../lib/upload-chat-attachments";

interface ThreadPanelProps {
    rootMessageId: string;
    targetMessageId?: string | null;
    onClose: () => void;
}

const HIGHLIGHT_HOLD_MS = 2200;

const styles = StyleSheet.create({
    highlightOutline: {
        position: "absolute",
        top: 0,
        right: 0,
        bottom: 0,
        left: 0,
        backgroundColor: "rgba(59, 130, 246, 0.05)",
        borderColor: "rgba(81, 162, 255, 0.28)",
        borderRadius: 16,
        borderWidth: 1,
        pointerEvents: "none",
    },
    highlightRail: {
        position: "absolute",
        left: 0,
        top: 8,
        bottom: 8,
        width: 3,
        borderRadius: 999,
        backgroundColor: "#51A2FF",
        pointerEvents: "none",
    },
});

const getRootPreview = (message: MessageDto) => {
    if (message.body) return message.body;
    if (message.attachments.some((attachment) => attachment.type === "Voice")) {
        return "Голосовое сообщение";
    }
    return "вложение";
};

export const ThreadPanel = ({
    rootMessageId,
    targetMessageId,
    onClose,
}: ThreadPanelProps) => {
    const messages = useThreadMessages(rootMessageId);
    const root = useThreadStore((s) => s.rootsById[rootMessageId]);
    const isLoading = useThreadStore((s) => !!s.loadingByThread[rootMessageId]);
    const hasMore = useThreadStore((s) => !!s.hasMoreByThread[rootMessageId]);
    const loadThread = useThreadStore((s) => s.loadThread);
    const loadMoreThread = useThreadStore((s) => s.loadMoreThread);
    const subscribeThread = useThreadStore((s) => s.subscribeThread);
    const unsubscribeThread = useThreadStore((s) => s.unsubscribeThread);
    const replyInThread = useThreadStore((s) => s.replyInThread);
    const listRef = useRef<FlatList<MessageDto>>(null);
    const [highlightedMessageId, setHighlightedMessageId] = useState<string | null>(null);

    const conversationRoot = useChatStore((s) => {
        for (const list of Object.values(s.messagesByConversation)) {
            const found = list.find((m) => m.id === rootMessageId);
            if (found) return found;
        }
        return undefined;
    });

    const currentUserId = useCurrentUserId();

    useEffect(() => {
        loadThread(rootMessageId, true);
        subscribeThread(rootMessageId);
        return () => {
            unsubscribeThread(rootMessageId);
        };
    }, [rootMessageId]);

    useEffect(() => {
        if (!targetMessageId) return;

        const index = messages.findIndex((message) => message.id === targetMessageId);
        if (index === -1) return;

        setHighlightedMessageId(targetMessageId);
        const scrollTimer = setTimeout(() => {
            listRef.current?.scrollToIndex({
                index,
                animated: true,
                viewPosition: 0.5,
            });
        }, 50);
        const clearTimer = setTimeout(() => {
            setHighlightedMessageId((current) =>
                current === targetMessageId ? null : current,
            );
        }, HIGHLIGHT_HOLD_MS);

        return () => {
            clearTimeout(scrollTimer);
            clearTimeout(clearTimer);
        };
    }, [messages, targetMessageId]);

    const rootMsg = root ?? conversationRoot;

    const handleSend = useCallback(async (
        text: string,
        files: DocumentPickerAsset[],
        _replyToMessageId?: string,
        _mentionUserIds?: string[],
        voiceDraft?: VoiceRecordingDraft | null,
    ) => {
        const trimmed = text.trim();
        if (!trimmed && files.length === 0 && !voiceDraft) return;
        try {
            const assetIds = await uploadChatAttachments(files, voiceDraft);
            await replyInThread(rootMessageId, trimmed, assetIds);
        } catch (e) {
            console.error("Reply in thread failed", e);
            throw e;
        }
    }, [rootMessageId, replyInThread]);

    const renderItem = useMemo(
        () =>
            ({ item }: { item: MessageDto }) => {
                const view = mapMessageToProps(item, currentUserId);
                const isHighlighted = highlightedMessageId === item.id;
                return (
                    <View className="px-4">
                        <View className="-mx-2 rounded-2xl px-2 py-1 overflow-hidden">
                            {isHighlighted ? (
                                <>
                                    <View
                                        testID={`thread-message-highlight-${view.id}`}
                                        style={styles.highlightOutline}
                                    />
                                    <View style={styles.highlightRail} />
                                </>
                            ) : null}
                            <ChatMessage
                                id={view.id}
                                text={view.text}
                                isOwn={view.isOwn}
                                kind={view.kind}
                                systemCall={view.systemCall}
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
                    </View>
                );
            },
        [currentUserId, highlightedMessageId],
    );

    return (
        <View className="flex-1 bg-app-card border-l border-white/5">
            <View className="flex-row items-center justify-between px-4 py-3 border-b border-white/5">
                <View>
                    <Text className="text-white text-base font-semibold">Тред</Text>
                    {rootMsg && (
                        <Text className="text-text-muted text-xs" numberOfLines={1}>
                            {getRootPreview(rootMsg)}
                        </Text>
                    )}
                </View>
                <Pressable onPress={onClose} hitSlop={8} className="p-1">
                    <XIcon size={20} className="text-text-subtle" />
                </Pressable>
            </View>

            <FlatList
                ref={listRef}
                className="flex-1 py-4"
                inverted
                data={messages}
                keyExtractor={(m) => m.id}
                renderItem={renderItem}
                contentContainerStyle={{ gap: 16 }}
                onScrollToIndexFailed={({ index }) => {
                    setTimeout(() => {
                        listRef.current?.scrollToIndex({
                            index,
                            animated: true,
                            viewPosition: 0.5,
                        });
                    }, 100);
                }}
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

            {rootMsg ? (
                <ChatInput
                    conversationId={rootMsg.conversationId}
                    onSend={handleSend}
                    typingEnabled={false}
                    composerMode="thread"
                />
            ) : null}
        </View>
    );
};
