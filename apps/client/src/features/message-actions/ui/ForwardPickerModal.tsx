import React, { useEffect, useMemo, useState } from "react";
import { FlatList, Pressable, Text, TextInput, View } from "react-native";
import { ModalOverlay, Avatar, EmptyState } from "@/shared/ui";
import { ChatsCircleIcon } from "@/shared/ui/phosphor";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useParticipantsStore } from "@/entities/conversation/model/participants-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { apiClient } from "@/shared/lib/api";
import type {
    ConversationParticipantDto,
    ConversationPreview,
    SearchUserDto,
} from "@urfu-link/api-client";

interface ForwardPickerModalProps {
    messageIds: string[] | null;
    onClose: () => void;
}

export const ForwardPickerModal = ({ messageIds, onClose }: ForwardPickerModalProps) => {
    const conversations = useChatStore((s) => s.conversations);
    const messagesByConversation = useChatStore((s) => s.messagesByConversation);
    const forwardMessages = useChatStore((s) => s.forwardMessages);
    const updateConversation = useChatStore((s) => s.updateConversation);
    const participantsByConversation = useParticipantsStore((s) => s.byConversationId);
    const loadParticipants = useParticipantsStore((s) => s.load);
    const primeParticipants = useParticipantsStore((s) => s.prime);
    const currentUserId = useCurrentUserId();
    const [query, setQuery] = useState("");
    const [userResults, setUserResults] = useState<SearchUserDto[]>([]);
    const [isSearchingUsers, setIsSearchingUsers] = useState(false);

    const visibleConversations = useMemo(
        () =>
            conversations.filter((conversation) => {
                if (conversation.type !== "Direct") return true;
                return (
                    conversation.lastMessagePreview != null ||
                    (messagesByConversation[conversation.id]?.length ?? 0) > 0
                );
            }),
        [conversations, messagesByConversation],
    );

    useEffect(() => {
        for (const conversation of visibleConversations) {
            if (conversation.type !== "Direct") continue;
            if (participantsByConversation[conversation.id]) continue;

            loadParticipants(conversation.id).catch(() => {
                /* fail-open: fallback title remains until participants can be loaded */
            });
        }
    }, [loadParticipants, participantsByConversation, visibleConversations]);

    useEffect(() => {
        const trimmed = query.trim();
        if (trimmed.length < 2) {
            setUserResults([]);
            setIsSearchingUsers(false);
            return;
        }

        const controller = new AbortController();
        setIsSearchingUsers(true);
        apiClient.users
            .searchUsers(trimmed, 0, 10, controller.signal)
            .then((response) => {
                setUserResults(response.items.filter((user) => user.id !== currentUserId));
            })
            .catch((error) => {
                if ((error as { name?: string }).name !== "AbortError") {
                    console.error("User search failed", error);
                }
            })
            .finally(() => setIsSearchingUsers(false));

        return () => controller.abort();
    }, [currentUserId, query]);

    if (!messageIds || messageIds.length === 0) return null;

    const handlePick = async (conversationId: string) => {
        try {
            await forwardMessages(conversationId, messageIds);
        } catch (e) {
            console.error("Forward failed", e);
        }
        onClose();
    };

    const handlePickUser = async (user: SearchUserDto) => {
        try {
            const conversation = await apiClient.chat.openDirectConversation(user.id);
            updateConversation(conversation);
            primeParticipants(conversation.id, [
                {
                    userId: user.id,
                    role: "Member",
                    displayName: user.displayName,
                    avatarUrl: user.avatarUrl ?? "",
                },
            ]);
            await forwardMessages(conversation.id, messageIds, user.id);
        } catch (error) {
            console.error("Forward to user failed", error);
        }
        onClose();
    };

    return (
        <ModalOverlay
            visible={messageIds.length > 0}
            onClose={onClose}
            contentClassName="bg-app-card border border-white/10 rounded-2xl w-[420px] max-w-[90%] max-h-[70vh] overflow-hidden"
        >
            <View className="px-4 py-3 border-b border-white/5 gap-3">
                <Text className="text-white text-base font-semibold">
                    ÐŸÐµÑ€ÐµÑÐ»Ð°Ñ‚ÑŒ Ð²â€¦
                </Text>
                <TextInput
                    testID="forward-user-search"
                    value={query}
                    onChangeText={setQuery}
                    placeholder="Найти пользователя"
                    placeholderTextColor="#90A1B9"
                    className="px-3 py-2 rounded-xl bg-white/5 border border-white/10 text-white"
                />
            </View>
            <FlatList
                data={visibleConversations}
                keyExtractor={(c) => c.id}
                ListHeaderComponent={
                    userResults.length > 0 || isSearchingUsers ? (
                        <View className="py-2 border-b border-white/5">
                            {isSearchingUsers ? (
                                <Text className="px-4 py-2 text-text-muted text-sm">
                                    Поиск...
                                </Text>
                            ) : null}
                            {userResults.map((user) => (
                                <Pressable
                                    key={user.id}
                                    testID={`forward-user-${user.id}`}
                                    onPress={() => void handlePickUser(user)}
                                    className="flex-row items-center gap-3 px-4 py-3 transition-colors hover:bg-white/5 active:bg-white/5"
                                >
                                    <Avatar size={36} src={user.avatarUrl ?? ""} name={user.displayName} />
                                    <View className="flex-1 min-w-0">
                                        <Text className="text-white text-[15px]" numberOfLines={1}>
                                            {user.displayName}
                                        </Text>
                                        <Text className="text-text-muted text-xs" numberOfLines={1}>
                                            @{user.username}
                                        </Text>
                                    </View>
                                </Pressable>
                            ))}
                        </View>
                    ) : null
                }
                renderItem={({ item }) => {
                    const peer = getDirectPeer(
                        item,
                        participantsByConversation[item.id]?.items,
                        currentUserId,
                    );
                    const name = resolveConversationName(item, peer);
                    const secondary = resolveConversationSecondary(item);
                    const avatarUrl =
                        item.type === "Direct" && peer?.avatarUrl?.trim()
                            ? peer.avatarUrl
                            : "";
                    return (
                        <Pressable
                            testID={`forward-conversation-${item.id}`}
                            onPress={() => handlePick(item.id)}
                            className="flex-row items-center gap-3 px-4 py-3 transition-colors hover:bg-white/5 active:bg-white/5"
                        >
                            <Avatar size={36} src={avatarUrl} name={name} />
                            <View className="flex-1 min-w-0">
                                <Text
                                    className="text-white text-[15px]"
                                    numberOfLines={1}
                                >
                                    {name}
                                </Text>
                                {secondary ? (
                                    <Text className="text-text-muted text-xs" numberOfLines={1}>
                                        {secondary}
                                    </Text>
                                ) : null}
                            </View>
                        </Pressable>
                    );
                }}
                ListEmptyComponent={
                    <EmptyState
                        size="compact"
                        icon={ChatsCircleIcon}
                        title="ÐÐµÑ‚ Ð´Ð¾ÑÑ‚ÑƒÐ¿Ð½Ñ‹Ñ… Ñ‡Ð°Ñ‚Ð¾Ð²"
                    />
                }
            />
        </ModalOverlay>
    );
};

const getDirectPeer = (
    conversation: ConversationPreview,
    participants: ConversationParticipantDto[] | undefined,
    currentUserId: string | null,
) => {
    if (conversation.type !== "Direct") return null;

    return (
        participants?.find((participant) => participant.userId !== currentUserId) ??
        participants?.[0] ??
        null
    );
};

const resolveConversationName = (
    conversation: ConversationPreview,
    peer: ConversationParticipantDto | null,
) => {
    if (conversation.type !== "Direct") {
        if (conversation.disciplineChatKind === "Subgroup") {
            return conversation.disciplineSubgroupName?.trim() || conversation.title || "Подгруппа";
        }

        return conversation.title ?? "Общий чат";
    }

    return peer?.displayName?.trim() || conversation.title || "Ð›Ð¸Ñ‡Ð½Ñ‹Ð¹ Ñ‡Ð°Ñ‚";
};

const resolveConversationSecondary = (conversation: ConversationPreview) => {
    if (conversation.type === "Direct") return null;
    const disciplineTitle = conversation.disciplineTitle?.trim() || "Дисциплина";
    return conversation.disciplineChatKind === "Subgroup"
        ? `${disciplineTitle} · Подгруппа`
        : `${disciplineTitle} · Общий чат`;
};
