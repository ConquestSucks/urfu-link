import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Avatar, Skeleton, StatusIndicator } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useConversationParticipants } from "@/entities/conversation/model/participants-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import React, { useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";
import { ChatHeaderActions } from "./ChatHeaderActions";
import { UserProfileModal } from "./UserProfileModal";
import {
    LastSeenLabel,
    TypingIndicator,
    presenceStatusToLabel,
    useConversationTypers,
    usePresenceStore,
    useUserPresence,
} from "@/entities/presence";
import type { UserStatus } from "@/shared/ui/StatusIndicator";

const presenceStatusToIndicator = (status?: string): UserStatus => {
    switch (status) {
        case "Online": return "online";
        case "Away": return "away";
        case "DoNotDisturb": return "doNotDisturb";
        default: return "offline";
    }
};

interface ChatHeaderProps {
    chatId: string;
    onOpenSearch?: () => void;
    onOpenPinned?: () => void;
}

export const ChatHeader = ({ chatId, onOpenSearch, onOpenPinned }: ChatHeaderProps) => {
    const [isProfileOpen, setIsProfileOpen] = useState(false);
    const { isMobile } = useWindowSize();
    const conversation = useChatStore((s) =>
        s.conversations.find((c) => c.id === chatId),
    );

    const participants = useConversationParticipants(chatId);
    const currentUserId = useCurrentUserId();

    // Для direct-чата peerUserId — тот, кто не равен currentUserId.
    // Заголовок и аватар берём из participants (lookup-кэш TTL 5 мин,
    // прогрев в ChatView через useParticipantsStore.load()).
    const peer = useMemo(() => {
        if (!conversation || conversation.type !== "Direct") return null;
        return (
            participants.find((p) => p.userId !== currentUserId) ??
            participants[0] ??
            null
        );
    }, [conversation, participants, currentUserId]);

    const presenceUserId = peer?.userId ?? "";
    const peerPresence = useUserPresence(presenceUserId);
    const watchUserPresence = usePresenceStore((s) => s.watchUserPresence);
    const unwatchUserPresence = usePresenceStore((s) => s.unwatchUserPresence);
    const typers = useConversationTypers(chatId, { excludeUserId: currentUserId });

    React.useEffect(() => {
        if (!peer?.userId) return;

        void watchUserPresence(peer.userId);
        return () => {
            unwatchUserPresence(peer.userId);
        };
    }, [peer?.userId, unwatchUserPresence, watchUserPresence]);

    if (!conversation) return null;

    const isDirectIdentityLoading =
        conversation.type === "Direct" && participants.length === 0;
    const chatName =
        isDirectIdentityLoading
            ? ""
            : conversation.title ??
              peer?.displayName ??
              (conversation.type === "Direct" ? "Личный чат" : "Чат");
    const chatAvatarUrl = isDirectIdentityLoading ? "" : peer?.avatarUrl ?? "";

    const isDirectPresenceLoading =
        conversation.type === "Direct" && (isDirectIdentityLoading || (!!peer?.userId && !peerPresence));
    const indicatorStatus = presenceStatusToIndicator(peerPresence?.status);
    const statusLabel = peerPresence
        ? presenceStatusToLabel(peerPresence.status)
        : null;

    return (
        <>
            <View className="flex-row justify-between items-center border-b border-white/5 pl-2.5 pr-3 py-2">
                <View className="flex-row gap-1 items-center flex-1 min-w-0">
                    {isMobile && (
                        <Pressable
                            onPress={() => safeGoBack("/chats")}
                            hitSlop={8}
                            className="p-2 rounded-xl"
                        >
                            <CaretLeftIcon size={24} className="text-text-subtle" weight="bold" />
                        </Pressable>
                    )}
                    <View className="flex-row gap-3 items-center">
                        <View className="relative z-1 p-0.5">
                            {isDirectIdentityLoading ? (
                                <Skeleton
                                    testID="chat-header-avatar-skeleton"
                                    style={{ width: 38, height: 38 }}
                                    className="rounded-xl"
                                />
                            ) : (
                                <Avatar size={38} src={chatAvatarUrl} name={chatName} />
                            )}
                            {isDirectPresenceLoading ? (
                                <View
                                    testID="chat-header-presence-dot-skeleton"
                                    className="absolute bottom-0 right-0 h-3 w-3 rounded-full border-2 border-app-card bg-white/10 animate-pulse"
                                />
                            ) : (
                                <StatusIndicator
                                    status={indicatorStatus}
                                    size={12}
                                    className="absolute bottom-0 right-0"
                                />
                            )}
                        </View>

                        <View className="justify-center flex-1 gap-1.5">
                            {isDirectIdentityLoading ? (
                                <Skeleton testID="chat-header-title-skeleton" className="h-4 w-36 rounded" />
                            ) : (
                                <Text
                                    numberOfLines={1}
                                    className="text-white leading-none text-base font-semibold"
                                >
                                    {chatName}
                                </Text>
                            )}
                            {(() => {
                                // Приоритет: typing > online/status label > last seen > "Не в сети".
                                if (isDirectIdentityLoading) {
                                    return (
                                        <Skeleton
                                            testID="chat-header-presence-skeleton"
                                            className="h-3 w-20 rounded"
                                        />
                                    );
                                }
                                if (typers.length > 0) {
                                    // В direct-чате имя печатающего совпадает с заголовком — не дублируем.
                                    // В групповом (Discipline) показываем "Иван печатает...".
                                    return (
                                        <TypingIndicator
                                            conversationId={chatId}
                                            showNames={conversation.type !== "Direct"}
                                            excludeUserId={currentUserId}
                                        />
                                    );
                                }
                                if (isDirectPresenceLoading) {
                                    return (
                                        <View
                                            testID="chat-header-presence-skeleton"
                                            className="h-3 w-20 rounded bg-white/10 animate-pulse"
                                        />
                                    );
                                }
                                if (indicatorStatus === "online" && statusLabel) {
                                    return (
                                        <Text
                                            numberOfLines={1}
                                            className="leading-none text-xs font-medium text-success-600"
                                        >
                                            {statusLabel}
                                        </Text>
                                    );
                                }
                                if (peerPresence?.lastSeenAt) {
                                    return (
                                        <LastSeenLabel
                                            lastSeenAt={peerPresence.lastSeenAt}
                                            className="leading-none text-xs font-medium text-text-muted"
                                        />
                                    );
                                }
                                return (
                                    <Text
                                        numberOfLines={1}
                                        className="leading-none text-xs font-medium text-text-muted"
                                    >
                                        {statusLabel ?? "Не в сети"}
                                    </Text>
                                );
                            })()}
                        </View>
                    </View>
                </View>

                <ChatHeaderActions
                    onOpenProfile={() => setIsProfileOpen(true)}
                    onOpenPinned={() => onOpenPinned?.()}
                    onSearchPress={() => onOpenSearch?.()}
                />
            </View>

            <UserProfileModal
                isOpen={isProfileOpen}
                onClose={() => setIsProfileOpen(false)}
                user={{
                    name: chatName,
                    avatarUrl: chatAvatarUrl,
                    status: peerPresence?.status,
                    lastSeenAt: peerPresence?.lastSeenAt,
                }}
            />
        </>
    );
};
