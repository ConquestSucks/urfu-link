import { ChatMessage } from "@/entities/chat-message";
import {
    mapMessageToProps,
    useChatStore,
    type LocalMessageDto,
} from "@/entities/conversation/model/chat-store";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { EmptyState } from "@/shared/ui";
import { ChatCircleTextIcon } from "@/shared/ui/phosphor";
import React, { forwardRef, useEffect, useImperativeHandle, useMemo, useRef } from "react";
import { FlatList, View } from "react-native";
import { ChatMessageSkeleton } from "@/entities/chat-message/ui/ChatMessageSkeleton";
import type { MessageDto } from "@urfu-link/api-client";
import { useCurrentUserId } from "@/shared/store/auth-store";

interface MessagesListProps {
    chatId: string;
    type: "chat" | "subject";
    onMessageLongPress?: (message: MessageDto) => void;
    onThreadOpen?: (rootMessageId: string) => void;
}

export interface MessagesListHandle {
    scrollToMessage: (messageId: string) => boolean;
}

export const MessagesList = forwardRef<MessagesListHandle, MessagesListProps>(
    ({ chatId, type, onMessageLongPress, onThreadOpen }, ref) => {
        const {
            messagesByConversation,
            isLoading,
            hasMoreByConversation,
            loadMessages,
            loadMore,
            markRead,
        } = useChatStore();
        const messages = messagesByConversation[chatId] || [];
        const hasMore = hasMoreByConversation[chatId] || false;
        const listRef = useRef<FlatList<MessageDto>>(null);

        const currentUserId = useCurrentUserId();

        useEffect(() => {
            loadMessages(chatId, type, true);
        }, [chatId, type]);

        useImperativeHandle(
            ref,
            () => ({
                scrollToMessage: (messageId: string) => {
                    const idx = messages.findIndex((m) => m.id === messageId);
                    if (idx === -1) return false;
                    listRef.current?.scrollToIndex({ index: idx, animated: true, viewPosition: 0.5 });
                    return true;
                },
            }),
            [messages],
        );

        const shouldShowAvatars = type === "subject";

        const isInitialLoading = isLoading && messages.length === 0;

        const handleViewableItemsChanged = React.useRef(
            ({
                viewableItems,
            }: {
                viewableItems: Array<{ item: MessageDto; index: number | null }>;
            }) => {
                const unreadItems = viewableItems.filter(
                    (v) => v.item.senderId !== currentUserId && v.item.readAt === null,
                );
                if (unreadItems.length > 0) {
                    const newestUnread = unreadItems.reduce((prev, curr) =>
                        (curr.index ?? 0) < (prev.index ?? 0) ? curr : prev,
                    );
                    markRead(chatId, newestUnread.item.id);
                }
            },
        ).current;

        const viewabilityConfig = React.useRef({ itemVisiblePercentThreshold: 50 }).current;

        const addReaction = useChatStore((s) => s.addReaction);
        const removeReaction = useChatStore((s) => s.removeReaction);

        const renderItem = useMemo(
            () =>
                ({ item }: { item: MessageDto }) => {
                    const view = mapMessageToProps(item, currentUserId);
                    const localStatus = (item as LocalMessageDto)._localStatus;
                    return (
                        <ChatMessage
                            id={view.id}
                            text={view.text}
                            isOwn={view.isOwn}
                            seen={view.seen}
                            time={view.time}
                            avatarUrl={view.avatarUrl}
                            showAvatar={shouldShowAvatars}
                            attachments={view.attachments}
                            replyTo={item.replyTo ?? null}
                            reactions={item.reactions}
                            editedAtUtc={item.editedAtUtc}
                            forwardedFrom={item.forwardedFrom ?? null}
                            isDeleted={item.state === "Deleted"}
                            threadReplyCount={item.threadReplyCount ?? 0}
                            localStatus={localStatus}
                            onLongPress={() => onMessageLongPress?.(item)}
                            onReactionPress={
                                currentUserId
                                    ? (emoji) => {
                                          const reacters = item.reactions?.[emoji] ?? [];
                                          if (reacters.includes(currentUserId)) {
                                              removeReaction(item.id, emoji);
                                          } else {
                                              addReaction(item.id, emoji);
                                          }
                                      }
                                    : undefined
                            }
                            onThreadOpen={
                                item.threadReplyCount && item.threadReplyCount > 0
                                    ? () => onThreadOpen?.(item.id)
                                    : undefined
                            }
                        />
                    );
                },
            [currentUserId, shouldShowAvatars, onMessageLongPress, onThreadOpen, addReaction, removeReaction],
        );

        if (isInitialLoading) {
            return (
                <View
                    className="flex-1 px-6 py-6 justify-end overflow-hidden"
                    style={{ gap: 24 }}
                >
                    <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                    <ChatMessageSkeleton isOwn={true} />
                    <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                    <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                    <ChatMessageSkeleton isOwn={true} />
                </View>
            );
        }

        // EmptyState вынесен за пределы FlatList: в inverted FlatList ListEmptyComponent
        // рендерится перевёрнутым на 180°. Early-return даёт корректную ориентацию.
        if (messages.length === 0) {
            return (
                <View className="flex-1 px-6 py-6 justify-end overflow-hidden">
                    <EmptyState
                        size="full"
                        icon={ChatCircleTextIcon}
                        title="Начните общение"
                        description="Отправьте первое сообщение в этом чате"
                    />
                </View>
            );
        }

        return (
            <FlatList
                ref={listRef}
                className="flex-1 px-6 py-6"
                inverted={true}
                data={messages}
                keyExtractor={(item) => item.id}
                contentContainerStyle={{
                    gap: 24,
                    flexGrow: 1,
                }}
                onViewableItemsChanged={handleViewableItemsChanged}
                viewabilityConfig={viewabilityConfig}
                renderItem={renderItem}
                onEndReached={() => loadMore(chatId, type)}
                onEndReachedThreshold={0.2}
                onScrollToIndexFailed={(info) => {
                    // Стандартный fallback для inverted FlatList: прокатываем к
                    // приблизительной позиции, ждём рендер и повторяем точный scrollToIndex.
                    const offset = info.averageItemLength * info.index;
                    listRef.current?.scrollToOffset({ offset, animated: false });
                    setTimeout(() => {
                        listRef.current?.scrollToIndex({
                            index: info.index,
                            viewPosition: 0.5,
                            animated: true,
                        });
                    }, 50);
                }}
                ListFooterComponent={() =>
                    isLoading && hasMore ? (
                        <View className="py-4">
                            <ActivityIndicator className="text-brand-600" />
                        </View>
                    ) : null
                }
            />
        );
    },
);

MessagesList.displayName = "MessagesList";
