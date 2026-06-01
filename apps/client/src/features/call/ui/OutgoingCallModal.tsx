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
import { useCurrentUser } from "@/entities/user";
import { startCallRingtone, stopCallRingtone } from "@/shared/lib/call-sounds";
import { ensureCallMediaAccess } from "@/features/call/lib/call-media-preflight";
import { Avatar, ModalOverlay } from "@/shared/ui";
import { PhoneDisconnectIcon, PhoneIcon, VideoCameraIcon } from "@/shared/ui/phosphor";
import { glow } from "@/shared/ui/glow";

export const OutgoingCallModal = () => {
    const router = useRouter();
    const currentUserId = useCurrentUserId();
    const { data: currentUser } = useCurrentUser();
    const outgoingCall = useCallStore((state) => state.outgoingCall);
    const activeCall = useCallStore((state) => state.activeCall);
    const cancelCall = useCallStore((state) => state.cancelCall);
    const [isCancelling, setIsCancelling] = useState(false);
    const [mediaError, setMediaError] = useState<string | null>(null);
    const outgoingCallIdRef = useRef<string | null>(null);
    const openedCallIdRef = useRef<string | null>(null);
    const keepModalOpen = useCallback(() => undefined, []);
    const visibleCall = outgoingCall ?? (mediaError && activeCall ? activeCall : null);
    const conversationId = visibleCall?.conversationId;
    const participants = useConversationParticipants(conversationId ?? "");
    const conversation = useChatStore((state) =>
        state.conversations.find((item) => item.id === conversationId),
    );

    useEffect(() => {
        if (!conversationId || participants.length > 0) return;

        void useParticipantsStore.getState().load(conversationId).catch(() => {
            // Best effort: fallback title remains available when participant metadata is unavailable.
        });
    }, [conversationId, participants.length]);

    useEffect(() => {
        if (!outgoingCall || outgoingCall.status !== "Ringing") {
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
        void (async () => {
            setMediaError(null);
            const media = await ensureCallMediaAccess({
                callType: activeCall.callType,
                soundVideo: currentUser?.soundVideo,
            });
            if (!media.ok) {
                setMediaError(media.errorMessage);
                return;
            }

            outgoingCallIdRef.current = null;
            await stopCallRingtone("outgoing");
            router.push(`/call/${activeCall.id}` as never);
        })();
    }, [activeCall, currentUser?.soundVideo, outgoingCall?.id, router]);

    const peer = useMemo(() => {
        if (!visibleCall) return null;

        const peerParticipant = participants.find((item) => item.userId !== currentUserId);
        return {
            name: peerParticipant?.displayName || conversation?.title || "Пользователь",
            avatarUrl: peerParticipant?.avatarUrl || null,
        };
    }, [conversation?.title, currentUserId, participants, visibleCall]);

    const callLabel = visibleCall?.callType === "Video" ? "Видеозвонок" : "Звонок";

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

    if (!visibleCall || (visibleCall.status !== "Ringing" && !mediaError)) {
        return null;
    }

    return (
        <ModalOverlay
            visible
            onClose={keepModalOpen}
            backdropClassName="bg-black/70 px-4"
            contentClassName="w-full max-w-[420px] rounded-3xl border border-white/15 bg-app-card p-5"
        >
            <View className="items-center gap-3">
                <View className="h-12 w-12 rounded-2xl bg-brand-650/15 border border-brand-300/10 items-center justify-center" style={glow.brandSubtle}>
                    {visibleCall.callType === "Video" ? (
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

            {mediaError ? (
                <View className="mt-4 rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-2">
                    <Text className="text-sm text-red-100">{mediaError}</Text>
                </View>
            ) : null}

            {outgoingCall ? (
                <Pressable
                    onPress={handleCancel}
                    disabled={isCancelling}
                    className="mt-5 h-12 rounded-2xl bg-red-600/90 items-center justify-center flex-row gap-2 disabled:opacity-60"
                    style={glow.danger}
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
            ) : null}
        </ModalOverlay>
    );
};
