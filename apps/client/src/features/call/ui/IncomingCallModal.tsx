import { useRouter } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Pressable, Text, View } from "react-native";
import { Avatar } from "@/shared/ui";
import { PhoneIcon, VideoCameraIcon } from "@/shared/ui/phosphor";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { useConversationParticipants, useParticipantsStore } from "@/entities/conversation/model/participants-store";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useCallStore } from "@/entities/call";
import type { CallSessionDto } from "@urfu-link/api-client";
import { startCallRingtone, stopCallRingtone } from "@/shared/lib/call-sounds";

export const IncomingCallModal = () => {
    const router = useRouter();
    const incomingCall = useCallStore((state) => state.incomingCall);
    const acceptIncoming = useCallStore((state) => state.acceptIncoming);
    const declineIncoming = useCallStore((state) => state.declineIncoming);
    const currentUserId = useCurrentUserId();
    const conversationId = incomingCall?.conversationId;
    const participants = useConversationParticipants(conversationId || "");
    const conversation = useChatStore((state) =>
        state.conversations.find((item) => item.id === conversationId),
    );

    const [isAccepting, setIsAccepting] = useState(false);
    const [isDeclining, setIsDeclining] = useState(false);

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
        try {
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
        <View
            className="absolute inset-0 z-50 justify-center items-center bg-black/70 px-4"
            style={{ position: "absolute" }}
        >
            <View className="w-full max-w-[520px] rounded-3xl border border-white/15 bg-app-card p-4">
                <View className="gap-3 mb-4">
                    <Text className="text-white text-xl font-semibold">Входящий {callLabel}</Text>
                    <View className="flex-row items-center gap-3">
                        <View className="rounded-xl overflow-hidden">
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

                <View className="flex-row gap-3">
                    <Pressable
                        onPress={handleAccept}
                        disabled={isAccepting || isDeclining}
                        className="flex-1 rounded-xl py-2.5 items-center bg-brand-600 disabled:opacity-60"
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
                    >
                        {isDeclining ? (
                            <ActivityIndicator size="small" color="#fff" />
                        ) : (
                            <Text className="text-white font-semibold">Отклонить</Text>
                        )}
                    </Pressable>
                </View>
            </View>
        </View>
    );
};
