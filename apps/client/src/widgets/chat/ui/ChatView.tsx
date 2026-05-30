import React, { useCallback, useEffect, useRef, useState } from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { ChatHeader } from "./chat-header/ChatHeader";
import { ChatInput, type ChatInputHandle } from "./chat-input/Input";
import { MessagesList, type MessagesListHandle } from "./MessagesList";
import { SubjectHeader } from "./subject-header/SubjectHeader";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useParticipantsStore } from "@/entities/conversation/model/participants-store";
import {
    isDirectDraftId,
    restoreDirectDraftConversation,
} from "@/entities/conversation/model/direct-draft-cache";
import { isDirectDraftConversation } from "@/entities/conversation/model/direct-draft-status";
import { LocalSearchPanel } from "@/features/chat-search";
import {
    ForwardPickerModal,
    MessageActionsMenu,
    ReactionPickerModal,
} from "@/features/message-actions";
import type { MessageDto, SearchResultDto } from "@urfu-link/api-client";
import { PinnedBar } from "./PinnedBar";
import { PinnedMessagesModal } from "./PinnedMessagesModal";
import { ThreadPanel } from "./thread/ThreadPanel";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { ModalOverlay } from "@/shared/ui";
import { apiClient } from "@/shared/lib/api";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { useCallStore } from "@/entities/call";
import {
    toChatThreadViewingContext,
    toChatViewingContext,
    usePresenceStore,
} from "@/entities/presence";
import { useConversationSend } from "../lib/use-conversation-send";

export const ChatView = () => {
    const { currentTab, params } = useInboxRouting();
    const { setPendingScrollToMessageId } = useChatStore();
    const startCall = useCallStore((state) => state.startCall);
    const conversation = useChatStore((s) =>
        s.conversations.find((c) => c.id === (params.id as string)),
    );
    const pendingScrollId = useChatStore((s) => s.pendingScrollToMessageId);
    const [isSearching, setIsSearching] = useState(false);
    const [actionsTarget, setActionsTarget] = useState<MessageDto | null>(null);
    const [actionsAnchor, setActionsAnchor] = useState<{ x: number; y: number } | null>(null);
    const [readStatusLabel, setReadStatusLabel] = useState<string | null>(null);
    const [isReadStatusLoading, setIsReadStatusLoading] = useState(false);
    const [reactionTargetId, setReactionTargetId] = useState<string | null>(null);
    const [forwardIds, setForwardIds] = useState<string[] | null>(null);
    const [openThreadRootId, setOpenThreadRootId] = useState<string | null>(null);
    const [isPinnedModalOpen, setIsPinnedModalOpen] = useState(false);
    const [isDocumentVisible, setIsDocumentVisible] = useState(() =>
        typeof document === "undefined" || document.visibilityState === "visible",
    );
    const { isMobile } = useWindowSize();
    const listRef = useRef<MessagesListHandle>(null);
    const inputRef = useRef<ChatInputHandle>(null);
    const currentUserId = useCurrentUserId();
    const [isStartingCall, setIsStartingCall] = useState(false);
    const setViewingContexts = usePresenceStore((s) => s.setViewingContexts);
    const chatId = params.id as string;
    const handleSend = useConversationSend(chatId);

    const handleStartAudioCall = useCallback(async () => {
        if (!conversation || conversation.type !== "Direct" || isStartingCall) return;
        try {
            setIsStartingCall(true);
            await startCall(conversation.id, "Audio");
        } finally {
            setIsStartingCall(false);
        }
    }, [conversation, isStartingCall, startCall]);

    const handleStartVideoCall = useCallback(async () => {
        if (!conversation || conversation.type !== "Direct" || isStartingCall) return;
        try {
            setIsStartingCall(true);
            await startCall(conversation.id, "Video");
        } finally {
            setIsStartingCall(false);
        }
    }, [conversation, isStartingCall, startCall]);

    const routeMessageId = typeof params.message === "string" ? params.message : null;
    const routeThreadRootId = typeof params.thread === "string" ? params.thread : null;
    const type = currentTab === "chats" ? "chat" : "subject";
    const isDirectDraft = isDirectDraftConversation(conversation);
    const isUnknownDirectDraft = type === "chat" && !conversation && isDirectDraftId(chatId);
    const shouldSkipRemoteConversationLoads = isDirectDraft || isUnknownDirectDraft;

    const handleSearchResultPress = useCallback(
        async (item: SearchResultDto) => {
            setIsSearching(false);
            const scrolled = await (listRef.current?.scrollToMessage(item.messageId) ?? Promise.resolve(false));
            if (!scrolled) setPendingScrollToMessageId(item.messageId);
        },
        [setPendingScrollToMessageId],
    );

    useEffect(() => {
        if (!pendingScrollId) return;
        let cancelled = false;
        void (async () => {
            const ok = await (listRef.current?.scrollToMessage(pendingScrollId) ?? Promise.resolve(false));
            if (ok && !cancelled) setPendingScrollToMessageId(null);
        })();
        return () => {
            cancelled = true;
        };
    }, [pendingScrollId, setPendingScrollToMessageId]);

    useEffect(() => {
        if (routeThreadRootId) {
            setOpenThreadRootId(routeThreadRootId);
            setPendingScrollToMessageId(routeThreadRootId);
            return;
        }

        if (routeMessageId) {
            setPendingScrollToMessageId(routeMessageId);
        }
    }, [routeMessageId, routeThreadRootId, setPendingScrollToMessageId]);

    useEffect(() => {
        if (type !== "chat" || conversation || !isDirectDraftId(chatId)) {
            return;
        }

        const restored = restoreDirectDraftConversation(chatId);
        if (!restored) {
            return;
        }

        useChatStore.getState().updateConversation(restored.conversation);
        useParticipantsStore.getState().prime(chatId, restored.participants);
    }, [chatId, conversation, type]);

    // Прогрев participants-store: нужен для @mentions autocomplete и для
    // resolve userId -> displayName в TypingIndicator. Кэш в сторе TTL 5 минут,
    // повторные load() для одного и того же chatId — no-op.
    useEffect(() => {
        if (!chatId) return;
        if (shouldSkipRemoteConversationLoads) {
            return;
        }

        useParticipantsStore
            .getState()
            .load(chatId)
            .catch(() => {
                /* fail-open: индикатор печати и mentions работают и без имён */
            });
    }, [chatId, shouldSkipRemoteConversationLoads]);

    useEffect(() => {
        if (typeof document === "undefined") {
            return;
        }

        const handleVisibilityChange = () => {
            setIsDocumentVisible(document.visibilityState === "visible");
        };

        document.addEventListener("visibilitychange", handleVisibilityChange);
        return () => {
            document.removeEventListener("visibilitychange", handleVisibilityChange);
        };
    }, []);

    useEffect(() => {
        if (!chatId || shouldSkipRemoteConversationLoads || !isDocumentVisible) {
            setViewingContexts([]);
            return;
        }

        const contexts = [toChatViewingContext(chatId)];
        if (openThreadRootId) {
            contexts.push(toChatThreadViewingContext(chatId, openThreadRootId));
        }

        setViewingContexts(contexts);
        return () => {
            setViewingContexts([]);
        };
    }, [
        chatId,
        isDocumentVisible,
        openThreadRootId,
        setViewingContexts,
        shouldSkipRemoteConversationLoads,
    ]);

    const pinnedIds = conversation?.pinnedMessageIds ?? [];
    const isActionsTargetPinned = !!(
        actionsTarget && pinnedIds.includes(actionsTarget.id)
    );
    const isActionsTargetOwn = !!currentUserId && actionsTarget?.senderId === currentUserId;

    const openActionsMenu = useCallback(
        (message: MessageDto, anchor: { x: number; y: number } | null = null) => {
            setActionsTarget(message);
            setActionsAnchor(anchor);
        },
        [],
    );

    const closeActionsMenu = useCallback(() => {
        setActionsTarget(null);
        setActionsAnchor(null);
        setReadStatusLabel(null);
        setIsReadStatusLoading(false);
    }, []);

    const jumpToMessage = useCallback(
        async (messageId: string) => {
            setIsPinnedModalOpen(false);
            const ok = await (listRef.current?.scrollToMessage(messageId) ?? Promise.resolve(false));
            if (!ok) setPendingScrollToMessageId(messageId);
        },
        [setPendingScrollToMessageId],
    );

    useEffect(() => {
        if (!actionsTarget || !isActionsTargetOwn) {
            setReadStatusLabel(null);
            setIsReadStatusLoading(false);
            return;
        }

        const formatTime = (value: string | null | undefined) => {
            if (!value) return "";
            const date = new Date(value);
            if (Number.isNaN(date.getTime())) return "";
            return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        };

        if (conversation?.type === "Direct") {
            const readTime = formatTime(actionsTarget.readAt);
            setReadStatusLabel(readTime ? `Прочитано ${readTime}` : "Не прочитано");
            setIsReadStatusLoading(false);
            return;
        }

        let cancelled = false;
        setIsReadStatusLoading(true);
        setReadStatusLabel(null);
        apiClient.chat
            .getReadReceipts(actionsTarget.id)
            .then((receipts) => {
                if (cancelled) return;
                if (receipts.length === 0) {
                    setReadStatusLabel("Не прочитано");
                    return;
                }
                const latest = receipts.reduce((prev, curr) =>
                    new Date(curr.readAtUtc).getTime() > new Date(prev.readAtUtc).getTime()
                        ? curr
                        : prev,
                );
                const readTime = formatTime(latest.readAtUtc);
                setReadStatusLabel(
                    readTime
                        ? `Прочитали ${receipts.length} • ${readTime}`
                        : `Прочитали ${receipts.length}`,
                );
            })
            .catch((error) => {
                console.error("Failed to load read receipts", error);
                if (!cancelled) setReadStatusLabel("Не прочитано");
            })
            .finally(() => {
                if (!cancelled) setIsReadStatusLoading(false);
            });

        return () => {
            cancelled = true;
        };
    }, [actionsTarget, conversation?.type, isActionsTargetOwn]);

    if (!chatId) return null;

    const mainColumn = (
        <View className="bg-app-card flex-1">
            {type === "chat" ? (
                isSearching ? (
                    <LocalSearchPanel
                        conversationId={chatId}
                        onResultPress={handleSearchResultPress}
                        onClose={() => setIsSearching(false)}
                    />
                ) : (
                    <ChatHeader
                        chatId={chatId}
                        onOpenSearch={() => setIsSearching(true)}
                        onOpenPinned={() => setIsPinnedModalOpen(true)}
                        onStartAudioCall={handleStartAudioCall}
                        onStartVideoCall={handleStartVideoCall}
                    />
                )
            ) : (
                <SubjectHeader
                    subjectId={chatId}
                    onOpenPinned={() => setIsPinnedModalOpen(true)}
                />
            )}

            {type === "chat" && pinnedIds.length > 0 && (
                <PinnedBar conversationId={chatId} listRef={listRef} />
            )}

            <MessagesList
                ref={listRef}
                chatId={chatId}
                type={type}
                skipInitialLoad={shouldSkipRemoteConversationLoads}
                onMessageLongPress={(message) => openActionsMenu(message)}
                onMessageContextMenu={openActionsMenu}
                onThreadOpen={setOpenThreadRootId}
                onFilesDropped={(files) => inputRef.current?.addFilesAndOpenModal(files)}
            />
            {!isUnknownDirectDraft && (
                <ChatInput
                    ref={inputRef}
                    conversationId={chatId}
                    onSend={handleSend}
                    typingEnabled={!isDirectDraft}
                />
            )}
        </View>
    );

    return (
        <View className="flex-1 flex-row">
            {mainColumn}

            {!isMobile && openThreadRootId && (
                <View className="w-[420px] max-w-[40%]">
                    <ThreadPanel
                        rootMessageId={openThreadRootId}
                        targetMessageId={
                            openThreadRootId === routeThreadRootId ? routeMessageId : null
                        }
                        onClose={() => setOpenThreadRootId(null)}
                    />
                </View>
            )}

            {isMobile && (
                <ModalOverlay
                    visible={!!openThreadRootId}
                    onClose={() => setOpenThreadRootId(null)}
                    contentClassName="bg-app-card w-full h-full"
                >
                    {openThreadRootId && (
                        <ThreadPanel
                            rootMessageId={openThreadRootId}
                            targetMessageId={
                                openThreadRootId === routeThreadRootId ? routeMessageId : null
                            }
                            onClose={() => setOpenThreadRootId(null)}
                        />
                    )}
                </ModalOverlay>
            )}

            <MessageActionsMenu
                message={actionsTarget}
                isOwn={isActionsTargetOwn}
                isPinned={isActionsTargetPinned}
                anchor={actionsAnchor}
                readStatusLabel={readStatusLabel}
                isReadStatusLoading={isReadStatusLoading}
                onClose={closeActionsMenu}
                onForwardRequest={() => {
                    if (actionsTarget) setForwardIds([actionsTarget.id]);
                    closeActionsMenu();
                }}
                onReactRequest={() => {
                    if (actionsTarget) setReactionTargetId(actionsTarget.id);
                    closeActionsMenu();
                }}
            />
            <ReactionPickerModal
                messageId={reactionTargetId}
                onClose={() => setReactionTargetId(null)}
            />
            <ForwardPickerModal
                messageIds={forwardIds}
                onClose={() => setForwardIds(null)}
            />
            <PinnedMessagesModal
                visible={isPinnedModalOpen}
                conversationId={chatId}
                onClose={() => setIsPinnedModalOpen(false)}
                onJumpToMessage={jumpToMessage}
            />
        </View>
    );
};
