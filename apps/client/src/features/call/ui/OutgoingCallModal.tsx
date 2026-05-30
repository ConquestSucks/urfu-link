import { useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ActivityIndicator, Pressable, Text, View } from "react-native";
import { useCallStore } from "@/entities/call";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import {
    useConversationParticipants,
    useParticipantsStore,
} from "@/entities/conversation/model/participants-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { startCallRingtone, stopCallRingtone } from "@/shared/lib/call-sounds";
import { Avatar } from "@/shared/ui";
import { PhoneDisconnectIcon, PhoneIcon, VideoCameraIcon } from "@/shared/ui/phosphor";

export const OutgoingCallModal = () => {
    const router = useRouter();
    const currentUserId = useCurrentUserId();
    const outgoingCall = useCallStore((state) => state.outgoingCall);
    const activeCall = useCallStore((state) => state.activeCall);
    const cancelCall = useCallStore((state) => state.cancelCall);
    const conversationId = outgoingCall?.conversationId;
    const participants = useConversationParticipants(conversationId ?? "");
    const conversation = useChatStore((state) =>
        state.conversations.find((item) => item.id === conversationId),
    );
    const [isCancelling, setIsCancelling] = useState(false);
    const outgoingCallIdRef = useRef<string | null>(null);
    const openedCallIdRef = useRef<string | null>(null);

    useEffect(() => {
        if (!conversationId || participants.length > 0) return;

        void useParticipantsStore.getState().load(conversationId).catch(() => {
            // Best effort: fallback title remains available when participant metadata is unavailable.
        });
    }, [conversationId, participants.length]);

    useEffect(() => {
        if (!outgoingCall) {
            if (!activeCall || activeCall.id !== outgoingCallIdRef.current) {
                outgoingCallIdRef.current = null;
            }
            void stopCallRingtone("outgoing");
            return;
        }

        outgoingCallIdRef.current = outgoingCall.id;
        void startCallRingtone("outgoing");
        return () => {
            void stopCallRingtone("outgoing");
        };
    }, [activeCall, outgoingCall]);

    useEffect(() => {
        const acceptedOutgoingCallId = outgoingCall?.id ?? outgoingCallIdRef.current;
        if (!activeCall || activeCall.id !== acceptedOutgoingCallId) return;
        if (openedCallIdRef.current === activeCall.id) return;

        openedCallIdRef.current = activeCall.id;
        outgoingCallIdRef.current = null;
        void stopCallRingtone("outgoing");
        router.push(`/call/${activeCall.id}` as never);
    }, [activeCall, outgoingCall?.id, router]);

    const peer = useMemo(() => {
        if (!outgoingCall) return null;

        const peerParticipant = participants.find((item) => item.userId !== currentUserId);
        return {
            name: peerParticipant?.displayName || conversation?.title || "Пользователь",
            avatarUrl: peerParticipant?.avatarUrl || null,
        };
    }, [conversation?.title, currentUserId, outgoingCall, participants]);

    const callLabel = outgoingCall?.callType === "Video" ? "Видеозвонок" : "Звонок";

    const handleCancel = useCallback(async () => {
        if (!outgoingCall || isCancelling) return;

        setIsCancelling(true);
        try {
            await cancelCall(outgoingCall.id);
            await stopCallRingtone("outgoing");
        } finally {
            setIsCancelling(false);
        }
    }, [cancelCall, isCancelling, outgoingCall]);

    if (!outgoingCall || outgoingCall.status !== "Ringing") {
        return null;
    }

    return (
        <View
            className="absolute inset-0 z-50 justify-center items-center bg-black/70 px-4"
            style={{ position: "absolute" }}
        >
            <View className="w-full max-w-[420px] rounded-3xl border border-white/15 bg-app-card p-5">
                <View className="items-center gap-3">
                    <View className="h-12 w-12 rounded-2xl bg-white/10 items-center justify-center">
                        {outgoingCall.callType === "Video" ? (
                            <VideoCameraIcon size={24} className="text-white" />
                        ) : (
                            <PhoneIcon size={24} className="text-white" />
                        )}
                    </View>

                    <Avatar size={72} src={peer?.avatarUrl} name={peer?.name ?? "U"} />

                    <View className="items-center gap-1">
                        <Text className="text-white text-xl font-semibold" numberOfLines={1}>
                            {peer?.name ?? "Пользователь"}
                        </Text>
                        <Text className="text-text-muted text-sm">{callLabel}</Text>
                        <Text className="text-white/80 text-sm">Устанавливаем соединение</Text>
                    </View>
                </View>

                <Pressable
                    onPress={handleCancel}
                    disabled={isCancelling}
                    className="mt-5 h-12 rounded-2xl bg-red-600/90 items-center justify-center flex-row gap-2 disabled:opacity-60"
                >
                    {isCancelling ? (
                        <ActivityIndicator size="small" color="#fff" />
                    ) : (
                        <>
                            <PhoneDisconnectIcon size={18} className="text-white" />
                            <Text className="text-white font-semibold">Отменить</Text>
                        </>
                    )}
                </Pressable>
            </View>
        </View>
    );
};
