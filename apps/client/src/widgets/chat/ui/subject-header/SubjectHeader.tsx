import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { useRouter } from "expo-router";
import { Avatar } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import {
    useConversationParticipants,
    useParticipantsStore,
} from "@/entities/conversation/model/participants-store";
import { saveDirectDraftConversation } from "@/entities/conversation/model/direct-draft-cache";
import {
    useCurrentUser,
    useMuteConversationNotifications,
    useUnmuteConversationNotifications,
} from "@/entities/user";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";
import { SubjectHeaderActions } from "./SubjectHeaderActions";
import { SubjectMembersModal, type SubjectMember } from "./SubjectMembersModal";
import { UserProfileModal } from "../chat-header/UserProfileModal";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { usePresenceStore } from "@/entities/presence";
import { useCallStore } from "@/entities/call";
import { apiClient } from "@/shared/lib/api";
import type { CallType, ConversationParticipantDto } from "@urfu-link/api-client";
import { glow } from "@/shared/ui/glow";

const roleLabel = (role: ConversationParticipantDto["role"]) =>
    role === "Teacher" ? "Преподаватель" : role === "Student" ? "Студент" : "Участник";

interface SubjectHeaderProps {
    subjectId: string;
    onOpenPinned?: () => void;
    onOpenSearch?: () => void;
    onStartGroupAudioCall?: () => void;
    onStartGroupVideoCall?: () => void;
    onBeforeStartCall?: () => void;
    profileUserId?: string | null;
    onProfileUserIdChange?: (userId: string | null) => void;
}

export const SubjectHeader = ({
    subjectId,
    onOpenPinned,
    onOpenSearch,
    onStartGroupAudioCall,
    onStartGroupVideoCall,
    onBeforeStartCall,
    profileUserId: controlledProfileUserId,
    onProfileUserIdChange,
}: SubjectHeaderProps) => {
    const [isMembersOpen, setIsMembersOpen] = useState(false);
    const [internalProfileUserId, setInternalProfileUserId] = useState<string | null>(null);
    const isProfileControlled = controlledProfileUserId !== undefined;
    const profileUserId = isProfileControlled ? controlledProfileUserId : internalProfileUserId;
    const setProfileUserId = useCallback(
        (userId: string | null) => {
            if (isProfileControlled) {
                onProfileUserIdChange?.(userId);
                return;
            }

            setInternalProfileUserId(userId);
        },
        [isProfileControlled, onProfileUserIdChange],
    );
    const router = useRouter();
    const { isMobile } = useWindowSize();
    const conversation = useChatStore((s) =>
        s.conversations.find((c) => c.id === subjectId),
    );
    const updateConversation = useChatStore((s) => s.updateConversation);
    const participants = useConversationParticipants(subjectId);
    const primeParticipants = useParticipantsStore((s) => s.prime);
    const startCall = useCallStore((s) => s.startCall);
    const currentUserId = useCurrentUserId();
    const presenceByUser = usePresenceStore((s) => s.presenceByUser);
    const watchUserPresence = usePresenceStore((s) => s.watchUserPresence);
    const unwatchUserPresence = usePresenceStore((s) => s.unwatchUserPresence);
    const loadBatchPresence = usePresenceStore((s) => s.loadBatchPresence);

    const participantIds = useMemo(
        () => participants.map((participant) => participant.userId).sort(),
        [participants],
    );
    const participantIdsKey = participantIds.join("|");

    useEffect(() => {
        if (participantIds.length === 0) return;
        void loadBatchPresence(participantIds).catch(() => undefined);
        for (const userId of participantIds) {
            void watchUserPresence(userId);
        }

        return () => {
            for (const userId of participantIds) {
                unwatchUserPresence(userId);
            }
        };
    }, [participantIdsKey, loadBatchPresence, participantIds, unwatchUserPresence, watchUserPresence]);

    const { data: profile } = useCurrentUser();
    const muteNotifications = useMuteConversationNotifications();
    const unmuteNotifications = useUnmuteConversationNotifications();
    const notificationsMuted =
        profile?.notifications.mutedConversationIds?.includes(subjectId) ?? false;
    const notificationsPending =
        muteNotifications.isPending || unmuteNotifications.isPending;
    const handleToggleNotifications = useCallback(() => {
        if (notificationsMuted) {
            unmuteNotifications.mutate(subjectId);
        } else {
            muteNotifications.mutate(subjectId);
        }
    }, [muteNotifications, notificationsMuted, subjectId, unmuteNotifications]);

    const selectedParticipant = participants.find((participant) => participant.userId === profileUserId) ?? null;
    const selectedPresence = profileUserId ? presenceByUser[profileUserId] : undefined;
    const isSelectedSelf = selectedParticipant?.userId === currentUserId;

    const openDirectConversationFromProfile = useCallback(async () => {
        if (!selectedParticipant || selectedParticipant.userId === currentUserId) {
            return null;
        }

        setIsMembersOpen(false);
        setProfileUserId(null);
        onBeforeStartCall?.();

        const direct = await apiClient.chat.openDirectConversation(selectedParticipant.userId);
        updateConversation(direct);

        const currentParticipant =
            currentUserId && profile
                ? {
                      userId: currentUserId,
                      role: "Member" as const,
                      displayName: profile.identity.name,
                      avatarUrl: profile.account.avatarUrl ?? "",
                  }
                : null;
        const directParticipants = [
            ...(currentParticipant ? [currentParticipant] : []),
            {
                userId: selectedParticipant.userId,
                role: selectedParticipant.role,
                displayName: selectedParticipant.displayName,
                avatarUrl: selectedParticipant.avatarUrl,
            },
        ];
        primeParticipants(direct.id, directParticipants);
        saveDirectDraftConversation(direct, directParticipants);

        return direct.id;
    }, [currentUserId, onBeforeStartCall, primeParticipants, profile, selectedParticipant, setProfileUserId, updateConversation]);

    const handleStartDirectCallFromProfile = useCallback(async (callType: CallType) => {
        const conversationId = await openDirectConversationFromProfile();
        if (!conversationId) {
            return;
        }

        await startCall(conversationId, callType);
    }, [openDirectConversationFromProfile, startCall]);

    const handleOpenDirectChatFromProfile = useCallback(async () => {
        setIsMembersOpen(false);

        const conversationId = await openDirectConversationFromProfile();
        if (!conversationId) {
            return;
        }

        router.push(`/chats/${encodeURIComponent(conversationId)}` as never);
    }, [openDirectConversationFromProfile, router]);

    if (!conversation) return null;

    const subjectName = conversation.disciplineChatKind === "Subgroup"
        ? `${conversation.disciplineTitle ?? "Дисциплина"} · ${conversation.disciplineSubgroupName ?? conversation.title ?? "Подгруппа"}`
        : `${conversation.disciplineTitle ?? conversation.title ?? "Дисциплина"} · Общий чат`;
    const subjectAvatarUrl = "";
    const onlineCount = participants.filter((participant) =>
        presenceByUser[participant.userId]?.status === "Online"
    ).length;
    const totalCount = participants.length;
    const membersData: SubjectMember[] = participants.map((participant) => ({
        id: participant.userId,
        name: participant.displayName || "Участник",
        avatarUrl: participant.avatarUrl,
        role: roleLabel(participant.role),
        isOnline: presenceByUser[participant.userId]?.status === "Online",
    }));
    const currentRole = currentUserId ? conversation.participantRoles?.[currentUserId] : undefined;
    const canStartDisciplineCall =
        conversation.groupSubtype === "Discipline" && currentRole === "Teacher";
    const canStartGroupAudioCall =
        conversation.capabilities?.canStartGroupAudioCall ??
        conversation.capabilities?.canStartGroupCall ??
        canStartDisciplineCall;
    const canStartGroupVideoCall =
        conversation.capabilities?.canStartGroupVideoCall ??
        conversation.capabilities?.canStartGroupCall ??
        canStartDisciplineCall;

    return (
        <>
            <View className="flex-row justify-between items-center border-b border-white/5 pl-2.5 pr-3 py-2" style={glow.surface}>
                <View className="flex-row gap-1 items-center flex-1 min-w-0">
                    {isMobile && (
                        <Pressable
                            onPress={() => safeGoBack("/subjects", { preferFallback: isMobile })}
                            hitSlop={8}
                            className="p-2 rounded-xl"
                        >
                            <CaretLeftIcon size={24} className="text-text-subtle" weight="bold" />
                        </Pressable>
                    )}
                    <View className="flex-row gap-3 items-center min-w-0 flex-1">
                        <View style={onlineCount > 0 ? glow.success : glow.brandSubtle}>
                            <Avatar size={38} src={subjectAvatarUrl} name={subjectName}/>
                        </View>
                        <View className="justify-center flex-1 gap-1.5 min-w-0">
                            <Text numberOfLines={1} className="text-white leading-none text-base font-semibold">
                                {subjectName}
                            </Text>
                            <Text
                                numberOfLines={1}
                                className={`leading-none text-xs font-medium ${onlineCount > 0 ? "text-success-600" : "text-text-muted"}`}
                            >
                                В сети {onlineCount} из {totalCount}
                            </Text>
                        </View>
                    </View>
                </View>

                <SubjectHeaderActions
                    canStartGroupAudioCall={canStartGroupAudioCall}
                    canStartGroupVideoCall={canStartGroupVideoCall}
                    onStartGroupAudioCall={onStartGroupAudioCall}
                    onStartGroupVideoCall={onStartGroupVideoCall}
                    onOpenSearch={onOpenSearch}
                    onOpenMembers={() => setIsMembersOpen(true)}
                    onOpenPinned={() => onOpenPinned?.()}
                    notificationsMuted={notificationsMuted}
                    notificationsPending={notificationsPending}
                    onToggleNotifications={handleToggleNotifications}
                />
            </View>

            <SubjectMembersModal
                isOpen={isMembersOpen}
                onClose={() => setIsMembersOpen(false)}
                members={membersData}
                onOpenProfile={(userId) => setProfileUserId(userId)}
            />
            <UserProfileModal
                isOpen={!!selectedParticipant}
                onClose={() => setProfileUserId(null)}
                user={{
                    name: selectedParticipant?.displayName || "Участник",
                    avatarUrl: selectedParticipant?.avatarUrl,
                    status: selectedPresence?.status,
                    lastSeenAt: selectedPresence?.lastSeenAt,
                }}
                onStartAudioCall={
                    selectedParticipant && !isSelectedSelf
                        ? () => void handleStartDirectCallFromProfile("Audio")
                        : undefined
                }
                onOpenChat={
                    selectedParticipant && !isSelectedSelf
                        ? () => void handleOpenDirectChatFromProfile()
                        : undefined
                }
                onToggleNotifications={handleToggleNotifications}
                notificationsMuted={notificationsMuted}
                notificationsPending={notificationsPending}
            />
        </>
    );
};
