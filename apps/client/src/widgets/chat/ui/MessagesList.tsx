import { ChatMessage, type MessageContextMenuAnchor } from "@/entities/chat-message";
import {
    mapMessageToProps,
    useChatStore,
    type LocalMessageDto,
} from "@/entities/conversation/model/chat-store";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { EmptyState } from "@/shared/ui";
import { ChatCircleTextIcon } from "@/shared/ui/phosphor";
import React, {
    forwardRef,
    useCallback,
    useEffect,
    useImperativeHandle,
    useMemo,
    useRef,
    useState,
} from "react";
import {
    Animated,
    FlatList,
    Platform,
    StyleSheet,
    Text,
    View,
    type NativeScrollEvent,
    type NativeSyntheticEvent,
} from "react-native";
import { ChatMessageSkeleton } from "@/entities/chat-message/ui/ChatMessageSkeleton";
import type { MessageDto } from "@urfu-link/api-client";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { TypingIndicator } from "@/entities/presence";
import { formatMessageDateLabel, getMessageDayKey } from "../lib/message-date-labels";

interface MessagesListProps {
    chatId: string;
    type: "chat" | "subject";
    skipInitialLoad?: boolean;
    onMessageLongPress?: (message: MessageDto) => void;
    onMessageContextMenu?: (message: MessageDto, anchor: MessageContextMenuAnchor) => void;
    onThreadOpen?: (rootMessageId: string) => void;
}

export interface MessagesListHandle {
    scrollToMessage: (messageId: string) => Promise<boolean>;
}

const HIGHLIGHT_FADE_IN_MS = 140;
const HIGHLIGHT_HOLD_MS = 1500;
const HIGHLIGHT_FADE_OUT_MS = 520;
const HIGHLIGHT_TOTAL_MS = HIGHLIGHT_FADE_IN_MS + HIGHLIGHT_HOLD_MS + HIGHLIGHT_FADE_OUT_MS;
const MAX_SCROLL_LOAD_ATTEMPTS = 30;
const SCROLL_DIRECTION_EPSILON = 8;
const NEWER_EDGE_OFFSET = 64;

const waitForListUpdate = () => new Promise((resolve) => setTimeout(resolve, 50));

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

export const MessagesList = forwardRef<MessagesListHandle, MessagesListProps>(
    (
        {
            chatId,
            type,
            skipInitialLoad = false,
            onMessageLongPress,
            onMessageContextMenu,
            onThreadOpen,
        },
        ref,
    ) => {
        const {
            messagesByConversation,
            messagesLoadingByConversation,
            messagesLoadedByConversation,
            hasMoreByConversation,
            loadMessages,
            loadMore,
            markRead,
        } = useChatStore();
        const messages = messagesByConversation[chatId] || [];
        const isLoading = messagesLoadingByConversation[chatId] || false;
        const isLoaded = messagesLoadedByConversation[chatId] || false;
        const hasMore = hasMoreByConversation[chatId] || false;
        const listRef = useRef<FlatList<MessageDto>>(null);
        const [highlightedMessageId, setHighlightedMessageId] = useState<string | null>(null);
        const highlightOpacity = useRef(new Animated.Value(0)).current;
        const highlightAnimationRef = useRef<Animated.CompositeAnimation | null>(null);
        const highlightClearTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
        const suppressEndReachedAfterScrollRef = useRef(false);
        const userDraggingAfterProgrammaticScrollRef = useRef(false);
        const lastScrollOffsetRef = useRef<number | null>(null);

        const currentUserId = useCurrentUserId();
        const readContextRef = useRef({ chatId, currentUserId, markRead });

        useEffect(() => {
            readContextRef.current = { chatId, currentUserId, markRead };
        }, [chatId, currentUserId, markRead]);

        useEffect(() => {
            if (skipInitialLoad) return;
            loadMessages(chatId, type, true);
        }, [chatId, type, skipInitialLoad, loadMessages]);

        useEffect(() => {
            return () => {
                highlightAnimationRef.current?.stop();
                if (highlightClearTimerRef.current) {
                    clearTimeout(highlightClearTimerRef.current);
                }
            };
        }, []);

        const highlightMessage = useCallback((messageId: string) => {
            const useNativeAnimationDriver = Platform.OS !== "web";
            highlightAnimationRef.current?.stop();
            if (highlightClearTimerRef.current) {
                clearTimeout(highlightClearTimerRef.current);
            }
            setHighlightedMessageId(messageId);
            highlightOpacity.stopAnimation();
            highlightOpacity.setValue(0);

            const animation = Animated.sequence([
                Animated.timing(highlightOpacity, {
                    toValue: 1,
                    duration: HIGHLIGHT_FADE_IN_MS,
                    useNativeDriver: useNativeAnimationDriver,
                }),
                Animated.delay(HIGHLIGHT_HOLD_MS),
                Animated.timing(highlightOpacity, {
                    toValue: 0,
                    duration: HIGHLIGHT_FADE_OUT_MS,
                    useNativeDriver: useNativeAnimationDriver,
                }),
            ]);

            highlightAnimationRef.current = animation;
            animation.start();
            highlightClearTimerRef.current = setTimeout(() => {
                highlightClearTimerRef.current = null;
                setHighlightedMessageId((current) => (current === messageId ? null : current));
            }, HIGHLIGHT_TOTAL_MS);
        }, [highlightOpacity]);

        const scrollLoadedMessageIntoView = useCallback(
            (messageId: string, sourceMessages: MessageDto[]) => {
                const idx = sourceMessages.findIndex((m) => m.id === messageId);
                if (idx === -1) return false;
                suppressEndReachedAfterScrollRef.current = true;
                userDraggingAfterProgrammaticScrollRef.current = false;
                lastScrollOffsetRef.current = null;
                listRef.current?.scrollToIndex({ index: idx, animated: true, viewPosition: 0.5 });
                highlightMessage(messageId);
                return true;
            },
            [highlightMessage],
        );

        const scrollToMessage = useCallback(
            async (messageId: string) => {
                if (scrollLoadedMessageIntoView(messageId, messages)) {
                    return true;
                }

                for (let attempt = 0; attempt < MAX_SCROLL_LOAD_ATTEMPTS; attempt++) {
                    const state = useChatStore.getState();
                    const latestMessages = state.messagesByConversation[chatId] ?? [];
                    if (scrollLoadedMessageIntoView(messageId, latestMessages)) {
                        return true;
                    }

                    if (state.messagesLoadingByConversation[chatId]) {
                        await waitForListUpdate();
                        continue;
                    }

                    if (!state.messagesLoadedByConversation[chatId] && !skipInitialLoad) {
                        await state.loadMessages(chatId, type, true);
                        await waitForListUpdate();
                        continue;
                    }

                    if (!state.hasMoreByConversation[chatId]) {
                        return false;
                    }

                    await state.loadMore(chatId, type);
                    await waitForListUpdate();
                }

                return false;
            },
            [chatId, messages, scrollLoadedMessageIntoView, skipInitialLoad, type],
        );

        useImperativeHandle(
            ref,
            () => ({
                scrollToMessage,
            }),
            [scrollToMessage],
        );

        const shouldShowAvatars = type === "subject";
        const isInitialLoading = isLoading && !isLoaded && messages.length === 0;

        const handleViewableItemsChanged = React.useRef(
            ({
                viewableItems,
            }: {
                viewableItems: Array<{ item: MessageDto; index: number | null }>;
            }) => {
                const {
                    chatId: activeChatId,
                    currentUserId: activeCurrentUserId,
                    markRead: activeMarkRead,
                } = readContextRef.current;
                if (!activeCurrentUserId) return;

                const unreadItems = viewableItems.filter(
                    (v) => v.item.senderId !== activeCurrentUserId && v.item.readAt === null,
                );
                if (unreadItems.length > 0) {
                    const newestUnread = unreadItems.reduce((prev, curr) =>
                        (curr.index ?? 0) < (prev.index ?? 0) ? curr : prev,
                    );
                    activeMarkRead(activeChatId, newestUnread.item.id);
                }
            },
        ).current;

        const viewabilityConfig = React.useRef({ itemVisiblePercentThreshold: 50 }).current;
        const addReaction = useChatStore((s) => s.addReaction);
        const removeReaction = useChatStore((s) => s.removeReaction);

        const handleScrollBeginDrag = useCallback(() => {
            if (!suppressEndReachedAfterScrollRef.current) return;
            userDraggingAfterProgrammaticScrollRef.current = true;
            lastScrollOffsetRef.current = null;
        }, []);

        const handleScroll = useCallback((event: NativeSyntheticEvent<NativeScrollEvent>) => {
            const offsetY = event.nativeEvent.contentOffset.y;
            const previousOffset = lastScrollOffsetRef.current;
            lastScrollOffsetRef.current = offsetY;

            if (
                !suppressEndReachedAfterScrollRef.current ||
                !userDraggingAfterProgrammaticScrollRef.current ||
                previousOffset === null
            ) {
                return;
            }

            const movedTowardOlderMessages = offsetY > previousOffset + SCROLL_DIRECTION_EPSILON;
            const returnedToNewestEdge = offsetY <= NEWER_EDGE_OFFSET;
            if (movedTowardOlderMessages || returnedToNewestEdge) {
                suppressEndReachedAfterScrollRef.current = false;
                userDraggingAfterProgrammaticScrollRef.current = false;
            }
        }, []);

        const handleEndReached = useCallback(() => {
            if (suppressEndReachedAfterScrollRef.current) return;
            loadMore(chatId, type);
        }, [chatId, loadMore, type]);

        const renderItem = useMemo(
            () =>
                ({ item, index }: { item: MessageDto; index: number }) => {
                    const view = mapMessageToProps(item, currentUserId);
                    const localStatus = (item as LocalMessageDto)._localStatus;
                    const isHighlighted = view.id === highlightedMessageId;
                    const next = messages[index + 1];
                    const currentDay = getMessageDayKey(item.createdAt);
                    const nextDay = getMessageDayKey(next?.createdAt);
                    const showDateSeparator = currentDay !== "" && currentDay !== nextDay;

                    return (
                        <View>
                            {showDateSeparator && (
                                <View className="items-center py-1">
                                    <View className="px-3 py-1.5 rounded-full bg-app-panel border border-white/10">
                                        <Text className="text-[12px] font-semibold text-text-subtle">
                                            {formatMessageDateLabel(item.createdAt)}
                                        </Text>
                                    </View>
                                </View>
                            )}
                            <View
                                testID={`message-row-${view.id}`}
                                className="-mx-2 rounded-2xl px-2 py-1 overflow-hidden"
                            >
                                {isHighlighted ? (
                                    <>
                                        <Animated.View
                                            testID={`message-highlight-${view.id}`}
                                            style={[
                                                styles.highlightOutline,
                                                { opacity: highlightOpacity },
                                            ]}
                                        />
                                        <Animated.View
                                            style={[
                                                styles.highlightRail,
                                                { opacity: highlightOpacity },
                                            ]}
                                        />
                                    </>
                                ) : null}
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
                                    isHighlighted={isHighlighted}
                                    threadReplyCount={item.threadReplyCount ?? 0}
                                    localStatus={localStatus}
                                    onLongPress={() => onMessageLongPress?.(item)}
                                    onContextMenu={(anchor) => onMessageContextMenu?.(item, anchor)}
                                    onReplyPress={
                                        item.replyTo?.messageId
                                            ? () => {
                                                  void scrollToMessage(item.replyTo!.messageId);
                                              }
                                            : undefined
                                    }
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
                            </View>
                        </View>
                    );
                },
            [
                currentUserId,
                highlightedMessageId,
                messages,
                shouldShowAvatars,
                onMessageLongPress,
                onMessageContextMenu,
                onThreadOpen,
                addReaction,
                removeReaction,
                scrollToMessage,
            ],
        );

        if (isInitialLoading) {
            return (
                <View className="flex-1 px-6 py-6 justify-end overflow-hidden" style={{ gap: 24 }}>
                    <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                    <ChatMessageSkeleton isOwn={true} />
                    <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                    <ChatMessageSkeleton isOwn={false} showAvatar={shouldShowAvatars} />
                    <ChatMessageSkeleton isOwn={true} />
                </View>
            );
        }

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
            <View className="flex-1">
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
                    onScroll={handleScroll}
                    onScrollBeginDrag={handleScrollBeginDrag}
                    scrollEventThrottle={16}
                    onEndReached={handleEndReached}
                    onEndReachedThreshold={0.2}
                    ListHeaderComponent={() => (
                        <TypingIndicator
                            conversationId={chatId}
                            showNames={type === "subject"}
                            excludeUserId={currentUserId}
                            variant="bubble"
                        />
                    )}
                    onScrollToIndexFailed={(info) => {
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
            </View>
        );
    },
);

MessagesList.displayName = "MessagesList";
