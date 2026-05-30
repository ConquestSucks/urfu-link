import { useLocalSearchParams, useRouter } from "expo-router";
import {
    ActivityIndicator,
    Platform,
    Pressable,
    ScrollView,
    Text,
    TextInput,
    View,
} from "react-native";
import {
    LiveKitRoom,
    useLocalParticipant,
    useParticipants,
    useTracks,
    VideoTrack,
} from "@livekit/react-native";
import { Track } from "livekit-client";
import { SafeAreaView } from "react-native-safe-area-context";
import { useCallback, useEffect, useMemo } from "react";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { useCallStore } from "@/entities/call";
import type { CallSessionDto, CallType } from "@urfu-link/api-client";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import {
    useConversationParticipants,
    useParticipantsStore,
} from "@/entities/conversation/model/participants-store";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { Avatar, ModalOverlay } from "@/shared/ui";
import {
    ChatCircleTextIcon,
    DotsThreeVerticalIcon,
    MicrophoneIcon,
    MicrophoneSlashIcon,
    PhoneDisconnectIcon,
    ScreencastIcon,
    VideoCameraIcon,
    XIcon,
} from "@/shared/ui/phosphor";

import { useState as useSafeState } from "react";

type CallPanel = "none" | "participants" | "chat";
type TrackRef = Parameters<typeof VideoTrack>[0]["trackRef"];
type DefinedTrackRef = Exclude<TrackRef, null | undefined>;

type ParticipantInfo = {
    userId: string;
    displayName: string;
    isConnected: boolean;
    isSelf: boolean;
};

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

const Panel = ({
    title,
    onClose,
    children,
}: {
    title: string;
    onClose: () => void;
    children: React.ReactNode;
}) => (
    <View className="w-full h-full bg-app-card border-l border-white/10">
        <View className="h-12 px-4 border-b border-white/10 flex-row items-center justify-between">
            <Text className="text-white font-semibold">{title}</Text>
            <Pressable
                className="h-8 w-8 rounded-full items-center justify-center bg-white/10"
                onPress={onClose}
            >
                <XIcon size={14} className="text-white" />
            </Pressable>
        </View>
        <View className="p-4">{children}</View>
    </View>
);

const SideAction = ({
    icon: Icon,
    label,
    isActive,
    disabled,
    onPress,
}: {
    icon: React.ComponentType<{ size?: string | number; className?: string }>;
    label: string;
    isActive?: boolean;
    disabled?: boolean;
    onPress?: () => void;
}) => (
    <Pressable
        onPress={onPress}
        disabled={disabled || !onPress}
        className={`h-12 rounded-xl px-3 flex-row items-center justify-center gap-2 flex-1 ${
            disabled
                ? "bg-white/5"
                : isActive
                  ? "bg-brand-600"
                  : "bg-white/10"
        }`}
    >
        <Icon size={18} className={disabled ? "text-white/50" : "text-white"} />
        <Text className={`text-xs font-semibold ${disabled ? "text-white/50" : "text-white"}`}>
            {label}
        </Text>
    </Pressable>
);

const CallControls = ({
    micEnabled,
    cameraEnabled,
    screenShareEnabled,
    callType,
    busy,
    onToggleMicrophone,
    onToggleCamera,
    onToggleScreenShare,
    onOpenChat,
    onOpenParticipants,
    onLeave,
}: {
    micEnabled: boolean;
    cameraEnabled: boolean;
    screenShareEnabled: boolean;
    callType: CallType;
    busy: boolean;
    onToggleMicrophone: () => Promise<void>;
    onToggleCamera: () => Promise<void>;
    onToggleScreenShare: () => Promise<void>;
    onOpenChat: () => void;
    onOpenParticipants: () => void;
    onLeave: () => void;
}) => (
    <View className="h-20 border-t border-white/10 px-3 pb-3 pt-2 flex-row gap-2 items-start">
        <SideAction
            icon={micEnabled ? MicrophoneIcon : MicrophoneSlashIcon}
            label="Микрофон"
            onPress={onToggleMicrophone}
            isActive={micEnabled}
            disabled={busy}
        />

        <SideAction
            icon={VideoCameraIcon}
            label="Камера"
            onPress={callType === "Video" ? onToggleCamera : undefined}
            isActive={callType === "Video" && cameraEnabled}
            disabled={busy || callType !== "Video"}
        />

        <SideAction
            icon={ScreencastIcon}
            label={Platform.OS === "web" ? "Демонстрация экрана" : "Демонстрация экрана"}
            onPress={
                Platform.OS === "web" && callType === "Video" ? onToggleScreenShare : undefined
            }
            isActive={callType === "Video" && screenShareEnabled}
            disabled={busy || Platform.OS !== "web" || callType !== "Video"}
        />

        <SideAction icon={ChatCircleTextIcon} label="Чат" onPress={onOpenChat} />
        <SideAction icon={DotsThreeVerticalIcon} label="Ещё" onPress={onOpenParticipants} />

        <Pressable
            onPress={onLeave}
            disabled={busy}
            className={`h-12 rounded-xl px-3 flex-1 min-w-24 flex-row items-center justify-center gap-2 bg-red-600 ${
                busy ? "opacity-70" : ""
            }`}
        >
            <PhoneDisconnectIcon size={18} className="text-white" />
            <Text className="text-xs font-semibold text-white">Завершить</Text>
        </Pressable>
    </View>
);

const VideoPanel = ({
    isVideoAvailable,
    mainTrack,
    thumbnailTracks,
    mainPlaceholderLabel,
}: {
    isVideoAvailable: boolean;
    mainTrack: DefinedTrackRef | null;
    thumbnailTracks: DefinedTrackRef[];
    mainPlaceholderLabel: string;
}) => {
    if (!isVideoAvailable || !mainTrack) {
        return (
            <View className="flex-1 bg-black/50 items-center justify-center px-10">
                <Avatar name={mainPlaceholderLabel} size={160} />
                <Text className="text-white mt-4 text-base">{mainPlaceholderLabel}</Text>
                <Text className="text-white/70 text-sm mt-2">Подключено к звонку без видео</Text>
            </View>
        );
    }

    return (
        <View className="relative flex-1 bg-black/30">
            <VideoTrack
                trackRef={mainTrack}
                style={{
                    width: "100%",
                    height: "100%",
                }}
            />

            {thumbnailTracks.length > 0 ? (
                <View className="absolute top-3 right-3 w-48 gap-2">
                    {thumbnailTracks.map((thumbnailTrack) => (
                            <View
                            key={`${thumbnailTrack.publication?.trackSid ?? "thumb"}-${thumbnailTrack.source}`}
                            className="w-full h-28 rounded-lg overflow-hidden border border-white/20"
                        >
                            <VideoTrack trackRef={thumbnailTrack} />
                        </View>
                    ))}
                </View>
            ) : null}
        </View>
    );
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

    const [callFromApi, setCallFromApi] = useSafeState<CallSessionDto | null>(null);
    const [loadError, setLoadError] = useSafeState<string | null>(null);
    const [isLeaving, setIsLeaving] = useSafeState(false);
    const [isPanelOpen, setIsPanelOpen] = useSafeState<CallPanel>("none");

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

    const [timerSeconds, setTimerSeconds] = useSafeState(0);

    const isConnectedToCall = call?.status === "Active";

    const { isMicrophoneEnabled, isCameraEnabled, isScreenShareEnabled, localParticipant } =
        useLocalParticipant();
    const participants = useParticipants();
    const cameraTracks = useTracks([Track.Source.Camera], {
        onlySubscribed: true,
    });

    const remoteCameraTracks = useMemo<DefinedTrackRef[]>(
        () =>
            cameraTracks.filter(
                (track): track is DefinedTrackRef =>
                    track !== null && track !== undefined && !track.participant.isLocal,
            ),
        [cameraTracks],
    );
    const localCameraTrack = useMemo<DefinedTrackRef | null>(() => {
        const local = cameraTracks.find((track) => track?.participant.isLocal);
        return (local as DefinedTrackRef | undefined) ?? null;
    }, [cameraTracks]);

    const mainCameraTrack = remoteCameraTracks.length > 0 ? remoteCameraTracks[0] : null;
    const thumbnailCameraTracks = useMemo(
        () => {
            const remoteThumbnails = remoteCameraTracks.slice(mainCameraTrack ? 1 : 0);

            if (!localCameraTrack) {
                return remoteThumbnails;
            }

            return [...remoteThumbnails, localCameraTrack];
        },
        [remoteCameraTracks, localCameraTrack, mainCameraTrack],
    );

    const isVideoCall = call?.callType === "Video";
    const canRenderVideo = isVideoCall && isConnectedToCall;

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
            return isConnectedToCall ? "Ожидание собеседника" : "Подключение к звонку";
        }

        if (isConnectedToCall) {
            return `${call.callType === "Video" ? "Видеозвонок" : "Звонок"} • ${formatDuration(
                timerSeconds,
            )}`;
        }

        return "Вызов завершён";
    }, [call, isConnectedToCall, timerSeconds]);

    const returnToConversation = useCallback(() => {
        if (!callConversationId) {
            router.replace("/chats");
            return;
        }

        router.replace(`/chats/${callConversationId}` as never);
    }, [callConversationId, router]);

    const toggleMicrophone = useCallback(async () => {
        if (!localParticipant) return;
        try {
            await localParticipant.setMicrophoneEnabled(!isMicrophoneEnabled);
        } catch (error) {
            console.error("Failed to toggle microphone", error);
        }
    }, [isMicrophoneEnabled, localParticipant]);

    const toggleCamera = useCallback(async () => {
        if (!localParticipant || !isVideoCall) return;
        try {
            await localParticipant.setCameraEnabled(!isCameraEnabled);
        } catch (error) {
            console.error("Failed to toggle camera", error);
        }
    }, [isVideoCall, isCameraEnabled, localParticipant]);

    const toggleScreenShare = useCallback(async () => {
        if (!localParticipant || !isVideoCall || Platform.OS !== "web") return;
        try {
            await localParticipant.setScreenShareEnabled(!isScreenShareEnabled);
        } catch (error) {
            console.error("Failed to toggle screen share", error);
        }
    }, [isScreenShareEnabled, isVideoCall, localParticipant]);

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
    }, [cancelCall, call, currentUserId, leaveCall, returnToConversation, setIsLeaving]);

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
                setLoadError(error instanceof Error ? error.message : "Не удалось загрузить звонок");
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
                    // token error is handled in the store and rendered in UI.
                });
            }
        })();
    }, [call, callToken, isTokenLoading, loadToken]);

    useEffect(() => {
        if (!callConversationId || call?.status !== "Ended") {
            return;
        }

        setIsPanelOpen("none");
        returnToConversation();
    }, [call?.status, callConversationId, returnToConversation]);

    useEffect(() => {
        if (!callConversationId) return;
        if (conversationParticipants.length > 0) return;

        void useParticipantsStore.getState().load(callConversationId).catch(() => {
            // fail-open: имена участников можно будет восстановить позднее.
        });
    }, [callConversationId, conversationParticipants.length]);

    useEffect(() => {
        if (!isConnectedToCall || !call?.acceptedAtUtc || isMobile) return;

        let timeout = setInterval(() => {
            const acceptedAt = Date.parse(call.acceptedAtUtc ?? "");
            if (Number.isNaN(acceptedAt)) {
                return;
            }

            setTimerSeconds(Math.max(0, Math.floor((Date.now() - acceptedAt) / 1000)));
        }, 1000);

        return () => {
            clearInterval(timeout);
        };
    }, [call?.acceptedAtUtc, isConnectedToCall, isMobile, setTimerSeconds]);

    if (!callId) {
        return <></>;
    }

    const showPanel = isPanelOpen !== "none";

    return (
        <SafeAreaView edges={["top", "left", "right", "bottom"]} className="flex-1 bg-black">
            <View className="flex-1 bg-app-bg">
                <View className="h-14 border-b border-white/10 px-4 flex-row items-center justify-between">
                    <View>
                        <Text className="text-white font-semibold">{callTitle}</Text>
                        <Text className="text-white/70 text-xs">{statusText}</Text>
                    </View>
                    <View className="flex-row items-center gap-2">
                        <Text className="text-white/70 text-xs">{participants.length} участн.</Text>
                    </View>
                </View>

                {call ? (
                    <LiveKitRoom
                        key={call.id}
                        serverUrl={callToken?.serverUrl}
                        token={callToken?.token}
                        audio={true}
                        video={isVideoCall && (isConnectedToCall || call.status === "Ringing")}
                        screen={
                            isVideoCall && Platform.OS === "web" && Boolean(isScreenShareEnabled)
                        }
                        connect={Boolean(callToken?.token)}
                        onDisconnected={() => {
                            // keep room auto-disconnecting on leave.
                        }}
                    >
                        <View className="flex-1">
                            <View className="flex-1 relative">
                                {tokenError || loadError ? (
                                    <ModalOverlay
                                        visible={true}
                                        onClose={() => {
                                            if (!isLeaving) {
                                                returnToConversation();
                                            }
                                        }}
                                        contentClassName="bg-app-card border border-white/10 rounded-2xl p-4 w-[360px]"
                                    >
                                        <Text className="text-white text-center">
                                            {tokenError || loadError}
                                        </Text>
                                    </ModalOverlay>
                                ) : null}

                                {isTokenLoading ? (
                                    <View className="absolute inset-0 items-center justify-center bg-black/70">
                                        <ActivityIndicator size="large" color="#fff" />
                                        <Text className="text-white mt-3">Подключение к звуковому каналу...</Text>
                                    </View>
                                ) : (
                                    <VideoPanel
                                        isVideoAvailable={canRenderVideo}
                                        mainTrack={mainCameraTrack}
                                        thumbnailTracks={thumbnailCameraTracks}
                                        mainPlaceholderLabel={
                                            callTitle
                                        }
                                    />
                                )}
                            </View>

                            <CallControls
                                micEnabled={isMicrophoneEnabled}
                                cameraEnabled={isCameraEnabled}
                                screenShareEnabled={isScreenShareEnabled}
                                callType={call.callType}
                                busy={isLeaving}
                                onToggleMicrophone={toggleMicrophone}
                                onToggleCamera={toggleCamera}
                                onToggleScreenShare={toggleScreenShare}
                                onOpenChat={() => setIsPanelOpen(isPanelOpen === "chat" ? "none" : "chat")}
                                onOpenParticipants={() => setIsPanelOpen(
                                    isPanelOpen === "participants" ? "none" : "participants",
                                )}
                                onLeave={handleLeave}
                            />

                            {showPanel ? (
                                <View
                                    className={`absolute inset-y-0 right-0 ${
                                        isMobile ? "w-full" : "w-80"
                                    } bg-app-card border-l border-white/10`}>
                                    {isPanelOpen === "participants" ? (
                                        <Panel title="Участники" onClose={() => setIsPanelOpen("none")}>
                                            {isMobile ? (
                                                <ScrollView>
                                                    <View className="gap-3">
                                                        {participantInfos.map((participant) => (
                                                            <View
                                                                key={participant.userId}
                                                                className="flex-row items-center gap-3"
                                                            >
                                                                <Avatar
                                                                    size={32}
                                                                    name={participant.displayName}
                                                                />
                                                                <View>
                                                                    <Text className="text-white">
                                                                        {participant.displayName}
                                                                    </Text>
                                                                    <Text className="text-xs text-text-muted">
                                                                        {participant.isConnected
                                                                            ? "В сети"
                                                                            : "Не в сети"}
                                                                    </Text>
                                                                </View>
                                                            </View>
                                                        ))}
                                                    </View>
                                                </ScrollView>
                                            ) : (
                                                <ScrollView>
                                                    <View className="gap-3">
                                                        {participantInfos.map((participant) => (
                                                            <View
                                                                key={participant.userId}
                                                                className="flex-row items-center gap-3"
                                                            >
                                                                <Avatar
                                                                    size={32}
                                                                    name={participant.displayName}
                                                                />
                                                                <View>
                                                                    <Text className="text-white">
                                                                        {participant.displayName}
                                                                    </Text>
                                                                    <Text className="text-xs text-text-muted">
                                                                        {participant.isConnected
                                                                            ? "В сети"
                                                                            : "Не в сети"}
                                                                    </Text>
                                                                </View>
                                                            </View>
                                                        ))}
                                                    </View>
                                                </ScrollView>
                                            )}
                                        </Panel>
                                    ) : (
                                        <Panel title="Чат" onClose={() => setIsPanelOpen("none")}>
                                            <Text className="text-white/80 mb-4">
                                                Чат во время звонка будет доступен в следующей версии.
                                            </Text>
                                            <TextInput
                                                placeholder="Написать сообщение"
                                                placeholderTextColor="#9ca3af"
                                                editable={false}
                                                className="px-3 py-3 rounded-lg bg-white/10 text-white"
                                            />
                                        </Panel>
                                    )}
                                </View>
                            ) : null}
                        </View>
                    </LiveKitRoom>
                ) : null}
            </View>
        </SafeAreaView>
    );
};
