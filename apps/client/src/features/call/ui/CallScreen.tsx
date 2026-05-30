import { useLocalSearchParams, useRouter } from "expo-router";
import { Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { useCallStore } from "@/entities/call";
import type { CallSessionDto } from "@urfu-link/api-client";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import {
    useConversationParticipants,
    useParticipantsStore,
} from "@/entities/conversation/model/participants-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { CallRoom } from "./CallRoom";
import type { ParticipantInfo } from "./CallRoom.types";

const normalizeId = (value: string | string[] | undefined): string | null => {
    if (Array.isArray(value)) return value[0] ?? null;
    return value ?? null;
};

const formatDuration = (value: number) => {
    const totalSeconds = Math.max(0, Math.floor(value));
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    if (hours > 0) {
        return `${hours.toString().padStart(2, "0")}:${minutes
            .toString()
            .padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
    }

    return `${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
};

export const CallScreen = () => {
    const { id } = useLocalSearchParams<{ id: string | string[] }>();
    const router = useRouter();
    const callId = normalizeId(id);
    const { isMobile } = useWindowSize();
    const currentUserId = useCurrentUserId();

    const loadCall = useCallStore((state) => state.loadCall);
    const setActiveCall = useCallStore((state) => state.setActiveCall);
    const clearToken = useCallStore((state) => state.clearToken);
    const loadToken = useCallStore((state) => state.loadToken);
    const leaveCall = useCallStore((state) => state.leaveCall);
    const cancelCall = useCallStore((state) => state.cancelCall);
    const incomingCall = useCallStore((state) => state.incomingCall);
    const activeCall = useCallStore((state) => state.activeCall);

    const callFromStore = useMemo(() => {
        if (!callId) return null;
        if (activeCall?.id === callId) return activeCall;
        if (incomingCall?.id === callId) return incomingCall;
        return null;
    }, [activeCall, callId, incomingCall]);

    const [callFromApi, setCallFromApi] = useState<CallSessionDto | null>(null);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [isLeaving, setIsLeaving] = useState(false);
    const [timerSeconds, setTimerSeconds] = useState(0);

    const call = callFromStore ?? callFromApi;
    const callConversationId = call?.conversationId ?? null;
    const conversation = useChatStore((state) =>
        callConversationId ? state.conversations.find((item) => item.id === callConversationId) : null,
    );
    const conversationParticipants = useConversationParticipants(callConversationId ?? "");
    const callToken = useCallStore((state) => (callId ? state.callTokens[callId] : undefined));
    const isTokenLoading = useCallStore((state) =>
        callId ? Boolean(state.tokenLoadingByCallId[callId]) : false,
    );
    const tokenError = useCallStore((state) => (callId ? state.tokenErrorByCallId[callId] : null));

    const isConnectedToCall = call?.status === "Active";

    const callTitle = useMemo(() => {
        if (conversation?.title) return conversation.title;

        const peer = conversationParticipants.find((p) => p.userId !== currentUserId);
        return peer?.displayName || "Видеозвонок";
    }, [conversation?.title, conversationParticipants, currentUserId]);

    const participantInfos = useMemo<ParticipantInfo[]>(() => {
        if (!call?.participantIds) return [];

        const connectedByUserId = new Map<string, boolean>(
            call.participants.map((item) => [item.userId, item.isConnected]),
        );

        return call.participantIds.map((userId) => {
            const conversationParticipant = conversationParticipants.find(
                (entry) => entry.userId === userId,
            );

            return {
                userId,
                displayName:
                    userId === currentUserId
                        ? "Вы"
                        : conversationParticipant?.displayName || "Участник",
                isSelf: userId === currentUserId,
                isConnected: Boolean(connectedByUserId.get(userId)),
            };
        });
    }, [call?.participantIds, call?.participants, conversationParticipants, currentUserId]);

    const statusText = useMemo(() => {
        if (!call) {
            return "Подключение...";
        }

        if (call.status === "Ringing") {
            return "Ожидание собеседника";
        }

        if (isConnectedToCall) {
            return `${call.callType === "Video" ? "Видеозвонок" : "Звонок"} • ${formatDuration(
                timerSeconds,
            )}`;
        }

        return "Вызов завершён";
    }, [call, isConnectedToCall, timerSeconds]);

    const connectedParticipantsCount = useMemo(() => {
        if (!call?.participants.length) return 0;
        return call.participants.filter((participant) => participant.isConnected).length;
    }, [call?.participants]);

    const returnToConversation = useCallback(() => {
        if (!callConversationId) {
            router.replace("/chats");
            return;
        }

        router.replace(`/chats/${callConversationId}` as never);
    }, [callConversationId, router]);

    const handleLeave = useCallback(async () => {
        if (!call) return;

        setIsLeaving(true);

        try {
            if (call.status === "Ringing" && call.callerId === currentUserId) {
                await cancelCall(call.id);
            } else {
                await leaveCall(call.id);
            }

            returnToConversation();
        } catch (error) {
            console.error("Failed to leave call", error);
        } finally {
            setIsLeaving(false);
        }
    }, [cancelCall, call, currentUserId, leaveCall, returnToConversation]);

    useEffect(() => {
        if (!callId) return;

        let isActive = true;

        void (async () => {
            if (callFromStore) {
                setCallFromApi(callFromStore);
                return;
            }

            try {
                const loaded = await loadCall(callId);
                setActiveCall(loaded);
                if (isActive) {
                    setCallFromApi(loaded);
                    setLoadError(null);
                }
            } catch (error) {
                if (!isActive) return;
                setLoadError(
                    error instanceof Error ? error.message : "Не удалось загрузить звонок",
                );
            }
        })();

        return () => {
            isActive = false;
        };
    }, [callFromStore, callId, loadCall, setActiveCall]);

    useEffect(() => {
        if (callId) {
            void clearToken(callId);
        }
    }, [callId, clearToken]);

    useEffect(() => {
        if (!call) return;

        void (async () => {
            if (call.status === "Ended") return;

            if (!callToken && !isTokenLoading) {
                await loadToken(call.id).catch(() => {
                    // Token error is stored and rendered by CallRoom.
                });
            }
        })();
    }, [call, callToken, isTokenLoading, loadToken]);

    useEffect(() => {
        if (!callConversationId || call?.status !== "Ended") {
            return;
        }

        returnToConversation();
    }, [call?.status, callConversationId, returnToConversation]);

    useEffect(() => {
        if (!callConversationId) return;
        if (conversationParticipants.length > 0) return;

        void useParticipantsStore.getState().load(callConversationId).catch(() => {
            // fail-open: participant names can be resolved later.
        });
    }, [callConversationId, conversationParticipants.length]);

    useEffect(() => {
        if (!isConnectedToCall || !call?.acceptedAtUtc || isMobile) return;

        const timeout = setInterval(() => {
            const acceptedAt = Date.parse(call.acceptedAtUtc ?? "");
            if (Number.isNaN(acceptedAt)) {
                return;
            }

            setTimerSeconds(Math.max(0, Math.floor((Date.now() - acceptedAt) / 1000)));
        }, 1000);

        return () => {
            clearInterval(timeout);
        };
    }, [call?.acceptedAtUtc, isConnectedToCall, isMobile]);

    if (!callId) {
        return null;
    }

    return (
        <SafeAreaView edges={["top", "left", "right", "bottom"]} className="flex-1 bg-black">
            <View className="flex-1 bg-app-bg">
                <View className="h-14 border-b border-white/10 px-4 flex-row items-center justify-between">
                    <View>
                        <Text className="text-white font-semibold">{callTitle}</Text>
                        <Text className="text-white/70 text-xs">{statusText}</Text>
                    </View>
                    <View className="flex-row items-center gap-2">
                        <Text className="text-white/70 text-xs">
                            {connectedParticipantsCount} участн.
                        </Text>
                    </View>
                </View>

                {call ? (
                    <CallRoom
                        call={call}
                        callToken={callToken}
                        isTokenLoading={isTokenLoading}
                        tokenError={tokenError}
                        loadError={loadError}
                        isLeaving={isLeaving}
                        isMobile={isMobile}
                        callTitle={callTitle}
                        participantInfos={participantInfos}
                        onLeave={handleLeave}
                        onCloseError={returnToConversation}
                    />
                ) : null}
            </View>
        </SafeAreaView>
    );
};
