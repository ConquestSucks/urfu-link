import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, Text, View } from "react-native";
import { Avatar, ModalOverlay } from "@/shared/ui";
import { PhoneIcon, VideoCameraIcon } from "@/shared/ui/phosphor";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { useCurrentUser } from "@/entities/user";
import { useConversationParticipants, useParticipantsStore } from "@/entities/conversation/model/participants-store";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useCallStore } from "@/entities/call";
import type { CallSessionDto } from "@urfu-link/api-client";
import { startCallRingtone, stopCallRingtone } from "@/shared/lib/call-sounds";
import { ensureCallMediaAccess } from "@/features/call/lib/call-media-preflight";
import { glow } from "@/shared/ui/glow";

export const IncomingCallModal = () => {
    const router = useRouter();
    const incomingCall = useCallStore((state) => state.incomingCall);
    const acceptIncoming = useCallStore((state) => state.acceptIncoming);
    const declineIncoming = useCallStore((state) => state.declineIncoming);
    const currentUserId = useCurrentUserId();
    const { data: currentUser } = useCurrentUser();
    const conversationId = incomingCall?.conversationId;
    const participants = useConversationParticipants(conversationId || "");
    const conversation = useChatStore((state) =>
        state.conversations.find((item) => item.id === conversationId),
    );

    const [isAccepting, setIsAccepting] = useState(false);
    const [isDeclining, setIsDeclining] = useState(false);
    const [mediaError, setMediaError] = useState<string | null>(null);
    const keepModalOpen = useCallback(() => undefined, []);

    useEffect(() => {
        if (!conversationId || participants.length > 0) {
            return;
        }

        void useParticipantsStore.getState().load(conversationId).catch(() => {
            // Best effort: имя абонента покажется как fallback при отсутствии кэша.
        });
    }, [conversationId, participants.length]);

    useEffect(() => {
        if (!incomingCall) {
            void stopCallRingtone("incoming");
            return;
        }

        void startCallRingtone("incoming");
        return () => {
            void stopCallRingtone("incoming");
        };
    }, [incomingCall?.id]);

    const caller = useMemo(() => {
        if (!incomingCall || !conversationId) {
            return null;
        }

        const peer = participants.find((item) => item.userId !== currentUserId);
        return {
            name: peer?.displayName || conversation?.title || "Личный чат",
            avatarUrl: peer?.avatarUrl || null,
            isDirect: true,
        };
    }, [conversationId, currentUserId, incomingCall, participants]);

    const callLabel = incomingCall?.callType === "Video" ? "Видеозвонок" : "Звонок";

    const handleAccept = async () => {
        if (!incomingCall || isAccepting || isDeclining) {
            return;
        }

        setIsAccepting(true);
        setMediaError(null);
        try {
            const media = await ensureCallMediaAccess({
                callType: incomingCall.callType,
                soundVideo: currentUser?.soundVideo,
            });
            if (!media.ok) {
                setMediaError(media.errorMessage);
                return;
            }

            await acceptIncoming();
            await stopCallRingtone("incoming");
            router.push(`/call/${incomingCall.id}` as never);
        } finally {
            setIsAccepting(false);
        }
    };

    const handleDecline = async () => {
        if (!incomingCall || isAccepting || isDeclining) {
            return;
        }

        setIsDeclining(true);
        try {
            await declineIncoming();
            await stopCallRingtone("incoming");
        } finally {
            setIsDeclining(false);
        }
    };

    if (!incomingCall) {
        return null;
    }

    return (
        <ModalOverlay
            visible
            onClose={keepModalOpen}
            backdropClassName="bg-black/70 px-4"
            contentClassName="w-full max-w-[520px] rounded-3xl border border-white/15 bg-app-card p-4"
        >
            <View className="gap-3 mb-4">
                <Text className="text-white text-xl font-semibold">Входящий {callLabel}</Text>
                <View className="flex-row items-center gap-3">
                    <View className="rounded-xl overflow-hidden bg-brand-650/10 p-3 border border-brand-300/10" style={glow.brandSubtle}>
                        {incomingCall.callType === "Video" ? (
                            <VideoCameraIcon size={24} className="text-text-subtle" />
                        ) : (
                            <PhoneIcon size={24} className="text-text-subtle" />
                        )}
                    </View>
                    <View className="flex-1">
                        <Text className="text-white text-lg font-semibold" numberOfLines={1}>
                            {caller?.name ?? "Вызов"}
                        </Text>
                        <Text className="text-text-muted">
                            Нажмите «Принять», чтобы подключиться
                        </Text>
                    </View>
                    <Avatar size={52} src={caller?.avatarUrl} name={caller?.name ?? "A"} />
                </View>
            </View>

            {mediaError ? (
                <View className="mb-3 rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-2">
                    <Text className="text-sm text-red-100">{mediaError}</Text>
                </View>
            ) : null}

            <View className="flex-row gap-3">
                <Pressable
                    onPress={handleAccept}
                    disabled={isAccepting || isDeclining}
                    className="flex-1 rounded-xl py-2.5 items-center bg-brand-650 disabled:opacity-60"
                    style={glow.brandSoft}
                >
                    {isAccepting ? (
                        <ActivityIndicator size="small" color="#fff" />
                    ) : (
                        <Text className="text-white font-semibold">Принять</Text>
                    )}
                </Pressable>

                <Pressable
                    onPress={handleDecline}
                    disabled={isAccepting || isDeclining}
                    className="flex-1 rounded-xl py-2.5 items-center bg-red-600/80 disabled:opacity-60"
                    style={glow.danger}
                >
                    {isDeclining ? (
                        <ActivityIndicator size="small" color="#fff" />
                    ) : (
                        <Text className="text-white font-semibold">Отклонить</Text>
                    )}
                </Pressable>
            </View>
        </ModalOverlay>
    );
};
