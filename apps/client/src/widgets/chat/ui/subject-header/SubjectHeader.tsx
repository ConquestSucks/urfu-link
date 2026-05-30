import { safeGoBack } from "@/shared/lib/safeGoBack";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { Avatar } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useConversationParticipants } from "@/entities/conversation/model/participants-store";
import { CaretLeftIcon } from "@/shared/ui/phosphor";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";
import { SubjectHeaderActions } from "./SubjectHeaderActions";
import { SubjectMembersModal, type SubjectMember } from "./SubjectMembersModal";
import { UserProfileModal } from "../chat-header/UserProfileModal";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { usePresenceStore } from "@/entities/presence";
import type { ConversationParticipantDto } from "@urfu-link/api-client";

const roleLabel = (role: ConversationParticipantDto["role"]) =>
    role === "Teacher" ? "Преподаватель" : role === "Student" ? "Студент" : "Участник";

export const SubjectHeader = ({ subjectId, onOpenPinned }: {
    subjectId: string;
    onOpenPinned?: () => void;
}) => {
    const [isMembersOpen, setIsMembersOpen] = useState(false);
    const [profileUserId, setProfileUserId] = useState<string | null>(null);
    const { isMobile } = useWindowSize();
    const conversation = useChatStore((s) =>
        s.conversations.find((c) => c.id === subjectId),
    );
    const participants = useConversationParticipants(subjectId);
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
    const selectedParticipant = participants.find((participant) => participant.userId === profileUserId) ?? null;
    const selectedPresence = profileUserId ? presenceByUser[profileUserId] : undefined;
    const currentRole = currentUserId ? conversation.participantRoles?.[currentUserId] : undefined;
    const canStartGroupCall =
        conversation.capabilities?.canStartGroupCall ??
        (conversation.disciplineChatKind === "Subgroup" && currentRole === "Teacher");

    const handleStartGroupCall = useCallback(() => {
        if (!canStartGroupCall || !subjectId) {
            return;
        }

        // TODO(#group-call): wire to real CallService adapter.
        console.warn(`Групповые звонки пока недоступны: ${subjectId}`);
    }, [canStartGroupCall, subjectId]);

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
                                В сети {onlineCount} из {totalCount}
                            </Text>
                        </View>
                    </View>
                </View>

                <SubjectHeaderActions
                    canStartGroupCall={canStartGroupCall}
                    onStartGroupCall={handleStartGroupCall}
                    onOpenMembers={() => setIsMembersOpen(true)}
                    onOpenPinned={() => onOpenPinned?.()}
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
            />
        </>
    );
};
