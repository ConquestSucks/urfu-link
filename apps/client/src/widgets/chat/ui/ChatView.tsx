import React, { useCallback, useEffect, useRef, useState } from "react";
import { View } from "react-native";
import { useInboxRouting } from "@/shared/lib/useInboxRouting";
import { ChatHeader } from "./chat-header/ChatHeader";
import { ChatInput } from "./chat-input/Input";
import { MessagesList, type MessagesListHandle } from "./MessagesList";
import { SubjectHeader } from "./subject-header/SubjectHeader";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { apiClient } from "@/shared/lib/api";
import type { DocumentPickerAsset } from "expo-document-picker";
import { LocalSearchPanel } from "@/features/chat-search";
import {
    EditMessageModal,
    ForwardPickerModal,
    MessageActionsMenu,
    ReactionPickerModal,
} from "@/features/message-actions";
import type { MessageDto, SearchResultDto } from "@urfu-link/api-client";
import { PinnedBar } from "./PinnedBar";
import { ThreadPanel } from "./thread/ThreadPanel";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { ModalOverlay } from "@/shared/ui";

export const ChatView = () => {
    const { currentTab, params } = useInboxRouting();
    const { sendMessage, setPendingScrollToMessageId } = useChatStore();
    const conversation = useChatStore((s) =>
        s.conversations.find((c) => c.id === (params.id as string)),
    );
    const pendingScrollId = useChatStore((s) => s.pendingScrollToMessageId);
    const [isSearching, setIsSearching] = useState(false);
    const [actionsTarget, setActionsTarget] = useState<MessageDto | null>(null);
    const [reactionTargetId, setReactionTargetId] = useState<string | null>(null);
    const [forwardIds, setForwardIds] = useState<string[] | null>(null);
    const [openThreadRootId, setOpenThreadRootId] = useState<string | null>(null);
    const { isMobile } = useWindowSize();
    const listRef = useRef<MessagesListHandle>(null);

    const chatId = params.id as string;
    const type = currentTab === "chats" ? "chat" : "subject";

    const handleSend = useCallback(
        async (text: string, files: DocumentPickerAsset[], replyToMessageId?: string) => {
            try {
                const assetIds: string[] = [];

                for (const file of files) {
                    const initRes = await apiClient.media.initUpload({
                        fileName: file.name,
                        size: file.size ?? 0,
                        mimeType: file.mimeType ?? "application/octet-stream",
                        visibility: "Private",
                    });

                    const blob = await fetch(file.uri).then((r) => r.blob());
                    await fetch(initRes.presignedPutUrl, {
                        method: "PUT",
                        body: blob,
                    });

                    await apiClient.media.completeUpload({ assetId: initRes.assetId });
                    assetIds.push(initRes.assetId);
                }

                await sendMessage(chatId, text, assetIds, replyToMessageId);
            } catch (error) {
                console.error("Failed to send message", error);
            }
        },
        [chatId, sendMessage],
    );

    const handleSearchResultPress = useCallback(
        (item: SearchResultDto) => {
            const scrolled = listRef.current?.scrollToMessage(item.messageId) ?? false;
            if (!scrolled) setPendingScrollToMessageId(item.messageId);
            setIsSearching(false);
        },
        [setPendingScrollToMessageId],
    );

    useEffect(() => {
        if (!pendingScrollId) return;
        const ok = listRef.current?.scrollToMessage(pendingScrollId);
        if (ok) setPendingScrollToMessageId(null);
    }, [pendingScrollId, setPendingScrollToMessageId]);

    if (!chatId) return null;

    const pinnedIds = conversation?.pinnedMessageIds ?? [];
    const isActionsTargetPinned = !!(
        actionsTarget && pinnedIds.includes(actionsTarget.id)
    );

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
                    <ChatHeader chatId={chatId} onOpenSearch={() => setIsSearching(true)} />
                )
            ) : (
                <SubjectHeader subjectId={chatId} />
            )}

            {type === "chat" && pinnedIds.length > 0 && (
                <PinnedBar conversationId={chatId} listRef={listRef} />
            )}

            <MessagesList
                ref={listRef}
                chatId={chatId}
                type={type}
                onMessageLongPress={setActionsTarget}
                onThreadOpen={setOpenThreadRootId}
            />
            <ChatInput conversationId={chatId} onSend={handleSend} />
        </View>
    );

    return (
        <View className="flex-1 flex-row">
            {mainColumn}

            {!isMobile && openThreadRootId && (
                <View className="w-[420px] max-w-[40%]">
                    <ThreadPanel
                        rootMessageId={openThreadRootId}
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
                            onClose={() => setOpenThreadRootId(null)}
                        />
                    )}
                </ModalOverlay>
            )}

            <MessageActionsMenu
                message={actionsTarget}
                isOwn={actionsTarget?.senderId === "me"}
                isPinned={isActionsTargetPinned}
                onClose={() => setActionsTarget(null)}
                onForwardRequest={() => {
                    if (actionsTarget) setForwardIds([actionsTarget.id]);
                    setActionsTarget(null);
                }}
                onReactRequest={() => {
                    if (actionsTarget) setReactionTargetId(actionsTarget.id);
                    setActionsTarget(null);
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
            <EditMessageModal />
        </View>
    );
};
