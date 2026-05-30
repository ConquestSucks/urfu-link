import { useLocalSearchParams, useRouter } from "expo-router";
import * as ScreenOrientation from "expo-screen-orientation";
import { ActivityIndicator, Text, View } from "react-native";
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
import { useCurrentUser } from "@/entities/user";
import { playCallSound } from "@/shared/lib/call-sounds";
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
    const { data: currentUser } = useCurrentUser();

    const loadCall = useCallStore((state) => state.loadCall);
    const setActiveCall = useCallStore((state) => state.setActiveCall);
    const clearToken = useCallStore((state) => state.clearToken);
    const loadToken = useCallStore((state) => state.loadToken);
    const leaveCall = useCallStore((state) => state.leaveCall);
    const cancelCall = useCallStore((state) => state.cancelCall);
    const incomingCall = useCallStore((state) => state.incomingCall);
    const outgoingCall = useCallStore((state) => state.outgoingCall);
    const activeCall = useCallStore((state) => state.activeCall);

    const callFromStore = useMemo(() => {
        if (!callId) return null;
        if (activeCall?.id === callId) return activeCall;
        if (incomingCall?.id === callId) return incomingCall;
        if (outgoingCall?.id === callId) return outgoingCall;
        return null;
    }, [activeCall, callId, incomingCall, outgoingCall]);

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
        return peer?.displayName || (call?.callType === "Video" ? "Видеозвонок" : "Звонок");
    }, [call?.callType, conversation?.title, conversationParticipants, currentUserId]);

    const participantInfos = useMemo<ParticipantInfo[]>(() => {
        if (!call?.participantIds) return [];

        const connectedByUserId = new Map<string, boolean>(
            call.participants.map((item) => [item.userId, item.isConnected]),
        );

        return call.participantIds.map((userId) => {
            const conversationParticipant = conversationParticipants.find(
                (entry) => entry.userId === userId,
            );
            const isSelf = userId === currentUserId;
            const displayName = conversationParticipant?.displayName?.trim()
                || (isSelf ? currentUser?.identity.name?.trim() : null)
                || "Участник";
            const avatarUrl = conversationParticipant?.avatarUrl
                || (isSelf ? currentUser?.account.avatarUrl : null)
                || null;

            return {
                userId,
                displayName,
                avatarUrl,
                isSelf,
                isConnected: Boolean(connectedByUserId.get(userId)),
            };
        });
    }, [
        call?.participantIds,
        call?.participants,
        conversationParticipants,
        currentUser?.account.avatarUrl,
        currentUser?.identity.name,
        currentUserId,
    ]);

    const statusText = useMemo(() => {
        if (!call) {
            return "Подключение...";
        }

        if (call.status === "Ringing") {
            return call.callerId === currentUserId
                ? "Ожидание ответа"
                : "Подключение к звонку";
        }

        if (isConnectedToCall) {
            return `${call.callType === "Video" ? "Видеозвонок" : "Звонок"} • ${formatDuration(
                timerSeconds,
            )}`;
        }

        return "Звонок завершён";
    }, [call, currentUserId, isConnectedToCall, timerSeconds]);

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
            await playCallSound("leave");
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
        void ScreenOrientation.lockAsync(ScreenOrientation.OrientationLock.ALL).catch(() => undefined);

        return () => {
            void ScreenOrientation.lockAsync(ScreenOrientation.OrientationLock.PORTRAIT_UP).catch(
                () => undefined,
            );
        };
    }, []);

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
                if (loaded.status === "Active") {
                    setActiveCall(loaded);
                }
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
        if (!call || call.status !== "Active") return;

        void (async () => {
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
        if (!isConnectedToCall || !call?.acceptedAtUtc) return;

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
    }, [call?.acceptedAtUtc, isConnectedToCall]);

    if (!callId) {
        return null;
    }

    return (
        <SafeAreaView edges={["top", "left", "right", "bottom"]} className="flex-1 bg-black">
            <View className="flex-1 bg-app-bg">
                <View className="h-14 border-b border-white/10 px-4 flex-row items-center justify-between">
                    <View className="flex-1 min-w-0">
                        <Text className="text-white font-semibold" numberOfLines={1}>
                            {callTitle}
                        </Text>
                        <Text className="text-white/70 text-xs" numberOfLines={1}>
                            {statusText}
                        </Text>
                    </View>
                    <View className="flex-row items-center gap-2">
                        <Text className="text-white/70 text-xs">
                            {call?.participantIds.length ?? 0} участн.
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
                        conversationId={call.conversationId}
                        participantInfos={participantInfos}
                        onLeave={handleLeave}
                        onCloseError={returnToConversation}
                    />
                ) : (
                    <View className="flex-1 items-center justify-center">
                        <ActivityIndicator size="large" color="#fff" />
                        <Text className="text-white mt-3">Подключение...</Text>
                    </View>
                )}
            </View>
        </SafeAreaView>
    );
};
