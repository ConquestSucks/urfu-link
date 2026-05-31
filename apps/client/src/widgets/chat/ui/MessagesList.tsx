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
    Pressable,
    StyleSheet,
    Text,
    View,
    type NativeScrollEvent,
    type NativeSyntheticEvent,
} from "react-native";
import { ChatMessageSkeleton } from "@/entities/chat-message/ui/ChatMessageSkeleton";
import type { ConversationParticipantDto, MessageDto } from "@urfu-link/api-client";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { TypingIndicator } from "@/entities/presence";
import { formatMessageDateLabel, getMessageDayKey } from "../lib/message-date-labels";
import { useWebFileDrop } from "@/shared/lib/useWebFileDrop";
import {
    useConversationParticipants,
    useParticipantsStore,
} from "@/entities/conversation/model/participants-store";
import { CaretDownIcon } from "@/shared/ui/phosphor";

interface MessagesListProps {
    chatId: string;
    type: "chat" | "subject";
    skipInitialLoad?: boolean;
    onMessageLongPress?: (message: MessageDto) => void;
    onMessageContextMenu?: (message: MessageDto, anchor: MessageContextMenuAnchor) => void;
    onAuthorPress?: (author: MessageAuthorProfile) => void;
    onThreadOpen?: (rootMessageId: string) => void;
    onFilesDropped?: (files: File[]) => void;
    onSystemCallJoin?: (callId: string) => void;
}

export interface MessagesListHandle {
    scrollToMessage: (messageId: string) => Promise<boolean>;
}

export type MessageAuthorProfile = Pick<
    ConversationParticipantDto,
    "userId" | "displayName" | "avatarUrl"
>;

const HIGHLIGHT_FADE_IN_MS = 140;
const HIGHLIGHT_HOLD_MS = 1500;
const HIGHLIGHT_FADE_OUT_MS = 520;
const HIGHLIGHT_TOTAL_MS = HIGHLIGHT_FADE_IN_MS + HIGHLIGHT_HOLD_MS + HIGHLIGHT_FADE_OUT_MS;
const MAX_SCROLL_LOAD_ATTEMPTS = 30;
const SCROLL_DIRECTION_EPSILON = 8;
const NEWER_EDGE_OFFSET = 64;
const SCROLL_TO_BOTTOM_BUTTON_OFFSET = 360;

type MessageListMessageItem = {
    type: "message";
    id: string;
    message: MessageDto;
};

type MessageListDateItem = {
    type: "date";
    id: string;
    dayKey: string;
    createdAt: string;
};

type MessageListItem = MessageListMessageItem | MessageListDateItem;

const buildMessageListItems = (messages: MessageDto[]): MessageListItem[] => {
    const items: MessageListItem[] = [];

    messages.forEach((message, index) => {
        items.push({
            type: "message",
            id: message.id,
            message,
        });

        const currentDay = getMessageDayKey(message.createdAt);
        const nextDay = getMessageDayKey(messages[index + 1]?.createdAt);
        if (!currentDay || currentDay === nextDay) return;

        items.push({
            type: "date",
            id: `date-${currentDay}-${index}`,
            dayKey: currentDay,
            createdAt: message.createdAt,
        });
    });

    return items;
};

const getMessageListItemKey = (item: MessageListItem) => item.id;

const MessageDateSeparator = ({ dayKey, createdAt }: { dayKey: string; createdAt: string }) => {
    const label = formatMessageDateLabel(createdAt);

    if (!dayKey || !label) return null;

    return (
        <View testID={`message-date-separator-${dayKey}`} className="items-center py-3">
            <View className="px-3 py-1.5 rounded-full bg-app-panel border border-white/10">
                <Text className="text-[12px] font-semibold text-text-subtle">
                    {label}
                </Text>
            </View>
        </View>
    );
};

const waitForListUpdate = () => new Promise((resolve) => setTimeout(resolve, 50));

const getJoinableSystemCallIds = (messages: MessageDto[]): Set<string> => {
    const latestByCallId = new Map<string, { status: string; timestamp: number; index: number }>();

    messages.forEach((message, index) => {
        const systemCall = message.kind === "SystemCall" ? message.systemCall : null;
        if (!systemCall?.callId) return;

        const parsed = Date.parse(message.createdAt);
        const timestamp = Number.isFinite(parsed) ? parsed : Number.NEGATIVE_INFINITY;
        const current = latestByCallId.get(systemCall.callId);
        if (
            !current ||
            timestamp > current.timestamp ||
            (timestamp === current.timestamp && index < current.index)
        ) {
            latestByCallId.set(systemCall.callId, {
                status: systemCall.status,
                timestamp,
                index,
            });
        }
    });

    const joinable = new Set<string>();
    latestByCallId.forEach((value, callId) => {
        if (value.status === "Started") {
            joinable.add(callId);
        }
    });
    return joinable;
};

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
    messageAfterMessage: {
        marginTop: 12,
    },
    fileDropActive: {
        backgroundColor: "rgba(43, 127, 255, 0.08)",
    },
    scrollToBottomButton: {
        position: "absolute",
        right: 24,
        bottom: 16,
        zIndex: 11,
        ...(Platform.OS === "web"
            ? {}
            : {
                  shadowColor: "#000",
                  shadowOffset: { width: 0, height: 8 },
                  shadowOpacity: 0.22,
                  shadowRadius: 16,
                  elevation: 6,
              }),
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
            onAuthorPress,
            onThreadOpen,
            onFilesDropped,
            onSystemCallJoin,
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
        const listItems = useMemo(() => buildMessageListItems(messages), [messages]);
        const listRef = useRef<FlatList<MessageListItem>>(null);
        const [highlightedMessageId, setHighlightedMessageId] = useState<string | null>(null);
        const [showScrollToBottom, setShowScrollToBottom] = useState(false);
        const highlightOpacity = useRef(new Animated.Value(0)).current;
        const highlightAnimationRef = useRef<Animated.CompositeAnimation | null>(null);
        const highlightClearTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
        const suppressEndReachedAfterScrollRef = useRef(false);
        const userDraggingAfterProgrammaticScrollRef = useRef(false);
        const lastScrollOffsetRef = useRef<number | null>(null);

        const currentUserId = useCurrentUserId();
        const participants = useConversationParticipants(chatId);
        const loadParticipants = useParticipantsStore((s) => s.load);
        const participantsByUserId = useMemo(
            () => new Map(participants.map((participant) => [participant.userId, participant])),
            [participants],
        );
        const readContextRef = useRef({ chatId, currentUserId, markRead });
        const handleFilesDropped = useCallback(
            (files: File[]) => {
                onFilesDropped?.(files);
            },
            [onFilesDropped],
        );
        const { dropRef, isDragging: isFileDragging } = useWebFileDrop({
            enabled: !!onFilesDropped,
            onFiles: handleFilesDropped,
        });
        useEffect(() => {
            readContextRef.current = { chatId, currentUserId, markRead };
        }, [chatId, currentUserId, markRead]);

        useEffect(() => {
            setShowScrollToBottom(false);
            lastScrollOffsetRef.current = null;
        }, [chatId]);

        useEffect(() => {
            if (skipInitialLoad) return;
            loadMessages(chatId, type, true);
        }, [chatId, type, skipInitialLoad, loadMessages]);

        useEffect(() => {
            if (type !== "subject" || participants.length > 0) return;
            loadParticipants(chatId).catch(() => undefined);
        }, [chatId, loadParticipants, participants.length, type]);

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
                const idx = buildMessageListItems(sourceMessages).findIndex(
                    (item) => item.type === "message" && item.message.id === messageId,
                );
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
        const joinableSystemCallIds = useMemo(
            () => (type === "subject" ? getJoinableSystemCallIds(messages) : new Set<string>()),
            [messages, type],
        );

        const handleViewableItemsChanged = React.useRef(
            ({
                viewableItems,
            }: {
                viewableItems: Array<{ item: MessageListItem; index: number | null }>;
            }) => {
                const {
                    chatId: activeChatId,
                    currentUserId: activeCurrentUserId,
                    markRead: activeMarkRead,
                } = readContextRef.current;
                if (!activeCurrentUserId) return;

                const unreadItems = viewableItems.filter(
                    (v): v is { item: MessageListMessageItem; index: number | null } =>
                        v.item.type === "message" &&
                        v.item.message.senderId !== activeCurrentUserId &&
                        v.item.message.readAt === null,
                );
                if (unreadItems.length > 0) {
                    const newestUnread = unreadItems.reduce((prev, curr) =>
                        (curr.index ?? 0) < (prev.index ?? 0) ? curr : prev,
                    );
                    activeMarkRead(activeChatId, newestUnread.item.message.id);
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
            const shouldShowScrollToBottom = offsetY > SCROLL_TO_BOTTOM_BUTTON_OFFSET;
            setShowScrollToBottom((current) =>
                current === shouldShowScrollToBottom ? current : shouldShowScrollToBottom,
            );

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

        const handleScrollToBottom = useCallback(() => {
            suppressEndReachedAfterScrollRef.current = false;
            userDraggingAfterProgrammaticScrollRef.current = false;
            lastScrollOffsetRef.current = 0;
            setShowScrollToBottom(false);
            listRef.current?.scrollToOffset({ offset: 0, animated: true });
        }, []);

        const handleEndReached = useCallback(() => {
            if (suppressEndReachedAfterScrollRef.current) return;
            loadMore(chatId, type);
        }, [chatId, loadMore, type]);

        const renderItem = useMemo(
            () =>
                ({ item, index }: { item: MessageListItem; index: number }) => {
                    if (item.type === "date") {
                        return (
                            <MessageDateSeparator
                                dayKey={item.dayKey}
                                createdAt={item.createdAt}
                            />
                        );
                    }

                    const message = item.message;
                    const view = mapMessageToProps(message, currentUserId);
                    const sender = participantsByUserId.get(message.senderId);
                    const authorName =
                        shouldShowAvatars && !view.isOwn
                            ? sender?.displayName?.trim() || message.senderId
                            : undefined;
                    const authorAvatarUrl = sender?.avatarUrl?.trim() || view.avatarUrl;
                    const authorProfile = authorName && sender
                        ? {
                              userId: sender.userId,
                              displayName: authorName,
                              avatarUrl: authorAvatarUrl,
                          }
                        : null;
                    const isSystemMessage = message.kind === "SystemCall";
                    const canJoinSystemCall =
                        isSystemMessage &&
                        !!view.systemCall?.callId &&
                        joinableSystemCallIds.has(view.systemCall.callId);
                    const localStatus = (message as LocalMessageDto)._localStatus;
                    const isHighlighted = view.id === highlightedMessageId;
                    const itemAbove = listItems[index + 1];
                    const messageSpacing =
                        itemAbove?.type === "message" ? styles.messageAfterMessage : null;

                    return (
                        <View style={messageSpacing}>
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
                                    avatarUrl={authorAvatarUrl}
                                    authorName={authorName}
                                    showAvatar={shouldShowAvatars}
                                    attachments={view.attachments}
                                    kind={view.kind}
                                    systemCall={view.systemCall}
                                    replyTo={message.replyTo ?? null}
                                    reactions={message.reactions}
                                    editedAtUtc={message.editedAtUtc}
                                    forwardedFrom={message.forwardedFrom ?? null}
                                    isDeleted={message.state === "Deleted"}
                                    isHighlighted={isHighlighted}
                                    threadReplyCount={message.threadReplyCount ?? 0}
                                    localStatus={localStatus}
                                    onLongPress={
                                        isSystemMessage ? undefined : () => onMessageLongPress?.(message)
                                    }
                                    onContextMenu={
                                        isSystemMessage
                                            ? undefined
                                            : (anchor) => onMessageContextMenu?.(message, anchor)
                                    }
                                    onAuthorPress={
                                        authorProfile && onAuthorPress
                                            ? () => onAuthorPress?.(authorProfile)
                                            : undefined
                                    }
                                    onReplyPress={
                                        message.replyTo?.messageId
                                            ? () => {
                                                  void scrollToMessage(message.replyTo!.messageId);
                                              }
                                            : undefined
                                    }
                                    onReactionPress={
                                        isSystemMessage
                                            ? undefined
                                            : (
                                        currentUserId
                                                ? (emoji) => {
                                                      const reacters = message.reactions?.[emoji] ?? [];
                                                      if (reacters.includes(currentUserId)) {
                                                          removeReaction(message.id, emoji);
                                                      } else {
                                                          addReaction(message.id, emoji);
                                                      }
                                                  }
                                                : undefined
                                              )
                                    }
                                    showJoinCallAction={canJoinSystemCall}
                                    onJoinCall={canJoinSystemCall ? onSystemCallJoin : undefined}
                                    onThreadOpen={
                                        message.threadReplyCount && message.threadReplyCount > 0
                                            ? () => onThreadOpen?.(message.id)
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
                joinableSystemCallIds,
                listItems,
                participantsByUserId,
                shouldShowAvatars,
                onMessageLongPress,
                onMessageContextMenu,
                onAuthorPress,
                onThreadOpen,
                onSystemCallJoin,
                addReaction,
                removeReaction,
                scrollToMessage,
            ],
        );

        if (isInitialLoading) {
            return (
                <View
                    ref={dropRef}
                    testID="messages-list-drop-target"
                    className="flex-1 px-6 py-6 justify-end overflow-hidden"
                    style={[{ gap: 24 }, isFileDragging ? styles.fileDropActive : null]}
                >
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
                <View
                    ref={dropRef}
                    testID="messages-list-drop-target"
                    className="flex-1 px-6 py-6 justify-end overflow-hidden"
                    style={isFileDragging ? styles.fileDropActive : null}
                >
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
            <View
                ref={dropRef}
                testID="messages-list-drop-target"
                className="flex-1"
                style={isFileDragging ? styles.fileDropActive : null}
            >
                <FlatList
                    ref={listRef}
                    className="flex-1 px-6 py-6"
                    inverted={true}
                    data={listItems}
                    keyExtractor={getMessageListItemKey}
                    contentContainerStyle={{
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
                {showScrollToBottom ? (
                    <Pressable
                        accessibilityLabel="Scroll to newest messages"
                        accessibilityRole="button"
                        testID="messages-scroll-to-bottom"
                        className="h-11 w-11 items-center justify-center rounded-full border border-white/10 bg-app-panel active:opacity-80"
                        style={styles.scrollToBottomButton}
                        onPress={handleScrollToBottom}
                    >
                        <CaretDownIcon size={22} weight="bold" className="text-text-subtle" />
                    </Pressable>
                ) : null}
            </View>
        );
    },
);

MessagesList.displayName = "MessagesList";
