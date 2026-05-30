import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Avatar } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import {
    useConversationParticipants,
    useParticipantsStore,
} from "@/entities/conversation/model/participants-store";
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

const roleLabel = (role: ConversationParticipantDto["role"]) =>
    role === "Teacher" ? "ÐŸÑ€ÐµÐ¿Ð¾Ð´Ð°Ð²Ð°Ñ‚ÐµÐ»ÑŒ" : role === "Student" ? "Ð¡Ñ‚ÑƒÐ´ÐµÐ½Ñ‚" : "Ð£Ñ‡Ð°ÑÑ‚Ð½Ð¸Ðº";

export const SubjectHeader = ({
    subjectId,
    onOpenPinned,
    onOpenSearch,
    onStartGroupAudioCall,
    onStartGroupVideoCall,
}: {
    subjectId: string;
    onOpenPinned?: () => void;
    onOpenSearch?: () => void;
    onStartGroupAudioCall?: () => void;
    onStartGroupVideoCall?: () => void;
}) => {
    const [isMembersOpen, setIsMembersOpen] = useState(false);
    const [profileUserId, setProfileUserId] = useState<string | null>(null);
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

    if (!conversation) return null;

    const subjectName = conversation.disciplineChatKind === "Subgroup"
        ? `${conversation.disciplineTitle ?? "Ð”Ð¸ÑÑ†Ð¸Ð¿Ð»Ð¸Ð½Ð°"} Â· ${conversation.disciplineSubgroupName ?? conversation.title ?? "ÐŸÐ¾Ð´Ð³Ñ€ÑƒÐ¿Ð¿Ð°"}`
        : `${conversation.disciplineTitle ?? conversation.title ?? "Ð”Ð¸ÑÑ†Ð¸Ð¿Ð»Ð¸Ð½Ð°"} Â· ÐžÐ±Ñ‰Ð¸Ð¹ Ñ‡Ð°Ñ‚`;
    const subjectAvatarUrl = "";
    const onlineCount = participants.filter((participant) =>
        presenceByUser[participant.userId]?.status === "Online"
    ).length;
    const totalCount = participants.length;
    const membersData: SubjectMember[] = participants.map((participant) => ({
        id: participant.userId,
        name: participant.displayName || "Ð£Ñ‡Ð°ÑÑ‚Ð½Ð¸Ðº",
        avatarUrl: participant.avatarUrl,
        role: roleLabel(participant.role),
        isOnline: presenceByUser[participant.userId]?.status === "Online",
    }));
    const selectedParticipant = participants.find((participant) => participant.userId === profileUserId) ?? null;
    const selectedPresence = profileUserId ? presenceByUser[profileUserId] : undefined;
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
    const isSelectedSelf = selectedParticipant?.userId === currentUserId;

    const handleStartDirectCallFromProfile = useCallback(async (callType: CallType) => {
        if (!selectedParticipant || selectedParticipant.userId === currentUserId) {
            return;
        }

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
        primeParticipants(direct.id, [
            ...(currentParticipant ? [currentParticipant] : []),
            {
                userId: selectedParticipant.userId,
                role: selectedParticipant.role,
                displayName: selectedParticipant.displayName,
                avatarUrl: selectedParticipant.avatarUrl,
            },
        ]);
        await startCall(direct.id, callType);
    }, [currentUserId, primeParticipants, profile, selectedParticipant, startCall, updateConversation]);

    return (
        <>
            <View className="flex-row justify-between items-center border-b border-white/5 pl-2.5 pr-3 py-2">
                <View className="flex-row gap-1 items-center flex-1 min-w-0">
                    {isMobile && (
                        <Pressable onPress={() => safeGoBack("/subjects")} hitSlop={8} className="p-2 rounded-xl">
                            <CaretLeftIcon size={24} className="text-text-subtle" weight="bold" />
                        </Pressable>
                    )}
                    <View className="flex-row gap-3 items-center min-w-0 flex-1">
                        <Avatar size={38} src={subjectAvatarUrl} name={subjectName}/>
                        <View className="justify-center flex-1 gap-1.5 min-w-0">
                            <Text numberOfLines={1} className="text-white leading-none text-base font-semibold">
                                {subjectName}
                            </Text>
                            <Text
                                numberOfLines={1}
                                className={`leading-none text-xs font-medium ${onlineCount > 0 ? "text-success-600" : "text-text-muted"}`}
                            >
                                Ð’ ÑÐµÑ‚Ð¸ {onlineCount} Ð¸Ð· {totalCount}
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
                    name: selectedParticipant?.displayName || "Ð£Ñ‡Ð°ÑÑ‚Ð½Ð¸Ðº",
                    avatarUrl: selectedParticipant?.avatarUrl,
                    status: selectedPresence?.status,
                    lastSeenAt: selectedPresence?.lastSeenAt,
                }}
                onStartAudioCall={
                    selectedParticipant && !isSelectedSelf
                        ? () => void handleStartDirectCallFromProfile("Audio")
                        : undefined
                }
                onStartVideoCall={
                    selectedParticipant && !isSelectedSelf
                        ? () => void handleStartDirectCallFromProfile("Video")
                        : undefined
                }
                onToggleNotifications={handleToggleNotifications}
                notificationsPending={notificationsPending}
            />
        </>
    );
};
