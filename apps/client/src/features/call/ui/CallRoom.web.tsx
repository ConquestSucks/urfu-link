import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Text, View } from "react-native";
import {
    ConnectionState,
    isBrowserSupported,
    LocalVideoTrack,
    RemoteVideoTrack,
    Room,
    RoomEvent,
    Track,
} from "livekit-client";
import { Avatar } from "@/shared/ui";
import { playCallSound } from "@/shared/lib/call-sounds";
import type { CallRoomProps, ParticipantInfo } from "./CallRoom.types";
import {
    CallControls,
    CallDrawer,
    CallErrorOverlay,
    CallLoadingOverlay,
    type CallPanel,
} from "./CallRoomControls";

type CameraFacingMode = "user" | "environment";

type WebVideoTrackRef = {
    key: string;
    ownerId: string;
    source: Track.Source;
    track: LocalVideoTrack | RemoteVideoTrack;
    isLocal: boolean;
};

type ParticipantMediaInfo = ParticipantInfo & {
    isMicrophoneEnabled: boolean;
    isCameraEnabled: boolean;
    isScreenShareEnabled: boolean;
    cameraTrack: WebVideoTrackRef | null;
    screenTrack: WebVideoTrackRef | null;
};

const collectVideoTracks = (room: Room, participantInfos: ParticipantInfo[]) => {
    const tracks: WebVideoTrackRef[] = [];
    const localInfo = participantInfos.find((participant) => participant.isSelf);

    room.remoteParticipants.forEach((participant) => {
        participant.videoTrackPublications.forEach((publication) => {
            const track = publication.videoTrack;
            if (!track || publication.isMuted) return;
            if (
                publication.source !== Track.Source.Camera &&
                publication.source !== Track.Source.ScreenShare
            ) {
                return;
            }

            tracks.push({
                key: `${participant.sid}-${publication.trackSid}`,
                ownerId: participant.identity,
                source: publication.source,
                track,
                isLocal: false,
            });
        });
    });

    room.localParticipant.videoTrackPublications.forEach((publication) => {
        const track = publication.videoTrack;
        if (!track || publication.isMuted) return;
        if (
            publication.source !== Track.Source.Camera &&
            publication.source !== Track.Source.ScreenShare
        ) {
            return;
        }

        tracks.push({
            key: `local-${publication.trackSid}`,
            ownerId: localInfo?.userId ?? room.localParticipant.identity,
            source: publication.source,
            track,
            isLocal: true,
        });
    });

    return tracks;
};

const disposeAudioElement = (element: HTMLMediaElement) => {
    element.pause();
    element.srcObject = null;
    element.remove();
};

const syncRemoteAudioTracks = (
    room: Room,
    audioElements: React.MutableRefObject<Map<string, HTMLMediaElement>>,
) => {
    const activeKeys = new Set<string>();

    room.remoteParticipants.forEach((participant) => {
        participant.audioTrackPublications.forEach((publication) => {
            const track = publication.audioTrack;
            if (!track || publication.isMuted) {
                return;
            }

            const key = `${participant.sid}-${publication.trackSid}`;
            activeKeys.add(key);

            if (audioElements.current.has(key)) {
                return;
            }

            const element = track.attach();
            element.autoplay = true;
            element.hidden = true;
            document.body.appendChild(element);
            audioElements.current.set(key, element);
            void element.play().catch((error) => {
                console.warn("LiveKit remote audio playback was blocked", error);
            });
        });
    });

    audioElements.current.forEach((element, key) => {
        if (activeKeys.has(key)) {
            return;
        }

        disposeAudioElement(element);
        audioElements.current.delete(key);
    });
};

const detachRemoteAudioTracks = (
    audioElements: React.MutableRefObject<Map<string, HTMLMediaElement>>,
) => {
    audioElements.current.forEach(disposeAudioElement);
    audioElements.current.clear();
};

const VideoElement = ({
    track,
    muted,
    mirrored,
}: {
    track: LocalVideoTrack | RemoteVideoTrack;
    muted?: boolean;
    mirrored?: boolean;
}) => {
    const ref = useRef<HTMLVideoElement | null>(null);

    useEffect(() => {
        const element = ref.current;
        if (!element) return;

        track.attach(element);
        void element.play().catch((error) => {
            console.warn("LiveKit video playback was blocked", error);
        });

        return () => {
            track.detach(element);
        };
    }, [track]);

    return React.createElement("video", {
        ref,
        autoPlay: true,
        muted,
        playsInline: true,
        style: {
            width: "100%",
            height: "100%",
            backgroundColor: "#000",
            objectFit: "cover",
            transform: mirrored ? "scaleX(-1)" : undefined,
        },
    });
};

const ParticipantTile = ({
    participant,
    isVideoCall,
    localFacingMode,
}: {
    participant: ParticipantMediaInfo;
    isVideoCall: boolean;
    localFacingMode: CameraFacingMode;
}) => {
    const visibleTrack = participant.screenTrack ?? participant.cameraTrack;
    const shouldMirror =
        participant.isSelf &&
        visibleTrack === participant.cameraTrack &&
        localFacingMode === "user";

    return (
        <View
            testID="call-participant-tile"
            className="flex-1 min-h-0 rounded-2xl overflow-hidden border border-white/10 bg-black/35"
        >
            <View className="flex-1 items-center justify-center bg-black/30">
                {isVideoCall && visibleTrack ? (
                    <VideoElement
                        track={visibleTrack.track}
                        muted={visibleTrack.isLocal}
                        mirrored={shouldMirror}
                    />
                ) : (
                    <View className="items-center gap-3">
                        <Avatar
                            size={96}
                            src={participant.avatarUrl}
                            name={participant.displayName}
                        />
                        <Text className="text-white text-base font-semibold">
                            {participant.displayName}
                        </Text>
                    </View>
                )}
            </View>

            <View className="absolute left-3 right-3 bottom-3 gap-2">
                <View className="self-start rounded-full bg-black/65 px-3 py-1">
                    <Text className="text-white text-xs font-semibold">
                        {participant.displayName}
                    </Text>
                </View>
                <View className="flex-row flex-wrap gap-1">
                    {!participant.isConnected ? (
                        <View className="rounded-full bg-white/10 px-2 py-1">
                            <Text className="text-white/80 text-[11px]">Подключается</Text>
                        </View>
                    ) : null}
                    {!participant.isMicrophoneEnabled ? (
                        <View className="rounded-full bg-white/10 px-2 py-1">
                            <Text className="text-white/80 text-[11px]">
                                Микрофон выключен
                            </Text>
                        </View>
                    ) : null}
                    {isVideoCall && !participant.isCameraEnabled ? (
                        <View className="rounded-full bg-white/10 px-2 py-1">
                            <Text className="text-white/80 text-[11px]">Камера выключена</Text>
                        </View>
                    ) : null}
                    {participant.isScreenShareEnabled ? (
                        <View className="rounded-full bg-brand-600/90 px-2 py-1">
                            <Text className="text-white text-[11px]">Демонстрация экрана</Text>
                        </View>
                    ) : null}
                </View>
            </View>
        </View>
    );
};

const CallStage = ({
    participants,
    isVideoCall,
    isMobile,
    localFacingMode,
}: {
    participants: ParticipantMediaInfo[];
    isVideoCall: boolean;
    isMobile: boolean;
    localFacingMode: CameraFacingMode;
}) => (
    <View className={`flex-1 p-3 gap-3 ${isMobile ? "" : "flex-row"}`}>
        {participants.map((participant) => (
            <ParticipantTile
                key={participant.userId}
                participant={participant}
                isVideoCall={isVideoCall}
                localFacingMode={localFacingMode}
            />
        ))}
    </View>
);

export const CallRoom = ({
    call,
    callToken,
    isTokenLoading,
    tokenError,
    loadError,
    isLeaving,
    isMobile,
    conversationId,
    participantInfos,
    onLeave,
    onCloseError,
}: CallRoomProps) => {
    const roomRef = useRef<Room | null>(null);
    const remoteAudioElementsRef = useRef(new Map<string, HTMLMediaElement>());
    const [panel, setPanel] = useState<CallPanel>("none");
    const [connectionState, setConnectionState] = useState<ConnectionState>(
        ConnectionState.Disconnected,
    );
    const [roomError, setRoomError] = useState<string | null>(null);
    const [isMicrophoneEnabled, setIsMicrophoneEnabled] = useState(false);
    const [isCameraEnabled, setIsCameraEnabled] = useState(false);
    const [isScreenShareEnabled, setIsScreenShareEnabled] = useState(false);
    const [videoTracks, setVideoTracks] = useState<WebVideoTrackRef[]>([]);
    const [facingMode, setFacingMode] = useState<CameraFacingMode>("user");

    const isVideoCall = call.callType === "Video";
    const isConnectedToCall = call.status === "Active";
    const canShareScreen = useMemo(
        () =>
            typeof navigator !== "undefined" &&
            Boolean(navigator.mediaDevices?.getDisplayMedia),
        [],
    );

    const syncRoomState = useCallback(() => {
        const room = roomRef.current;
        if (!room) {
            setConnectionState(ConnectionState.Disconnected);
            setIsMicrophoneEnabled(false);
            setIsCameraEnabled(false);
            setIsScreenShareEnabled(false);
            setVideoTracks([]);
            return;
        }

        setConnectionState(room.state);
        setIsMicrophoneEnabled(room.localParticipant.isMicrophoneEnabled);
        setIsCameraEnabled(room.localParticipant.isCameraEnabled);
        setIsScreenShareEnabled(room.localParticipant.isScreenShareEnabled);
        setVideoTracks(collectVideoTracks(room, participantInfos));
        syncRemoteAudioTracks(room, remoteAudioElementsRef);
    }, [participantInfos]);

    useEffect(() => {
        if (!callToken?.serverUrl || !callToken.token || call.status !== "Active") {
            return;
        }

        if (!isBrowserSupported()) {
            setRoomError("Браузер не поддерживает WebRTC.");
            return;
        }

        let isDisposed = false;
        const room = new Room({
            adaptiveStream: true,
            dynacast: true,
        });
        roomRef.current = room;
        setRoomError(null);

        const onRoomChanged = () => {
            if (!isDisposed) {
                syncRoomState();
            }
        };

        room.on(RoomEvent.Connected, onRoomChanged);
        room.on(RoomEvent.ConnectionStateChanged, onRoomChanged);
        room.on(RoomEvent.ParticipantConnected, onRoomChanged);
        room.on(RoomEvent.ParticipantDisconnected, onRoomChanged);
        room.on(RoomEvent.TrackSubscribed, onRoomChanged);
        room.on(RoomEvent.TrackUnsubscribed, onRoomChanged);
        room.on(RoomEvent.TrackMuted, onRoomChanged);
        room.on(RoomEvent.TrackUnmuted, onRoomChanged);
        room.on(RoomEvent.LocalTrackPublished, onRoomChanged);
        room.on(RoomEvent.LocalTrackUnpublished, onRoomChanged);
        room.on(RoomEvent.Disconnected, onRoomChanged);

        void (async () => {
            try {
                await room.connect(callToken.serverUrl, callToken.token, {
                    autoSubscribe: true,
                });
                if (isDisposed) return;

                await room.startAudio().catch((error) => {
                    console.warn("LiveKit audio start was blocked", error);
                });
                await room.localParticipant.setMicrophoneEnabled(true).catch((error) => {
                    console.error("Failed to enable microphone", error);
                    setRoomError("Не удалось включить микрофон. Проверьте разрешения браузера.");
                });

                if (call.callType === "Video") {
                    await room.localParticipant.setCameraEnabled(true, { facingMode }).catch((error) => {
                        console.error("Failed to enable camera", error);
                        setRoomError("Не удалось включить камеру. Проверьте разрешения браузера.");
                    });
                }

                syncRoomState();
            } catch (error) {
                if (!isDisposed) {
                    console.error("Failed to connect LiveKit room", error);
                    setRoomError(
                        error instanceof Error
                            ? error.message
                            : "Не удалось подключиться к звонку.",
                    );
                }
            }
        })();

        return () => {
            isDisposed = true;
            room.off(RoomEvent.Connected, onRoomChanged);
            room.off(RoomEvent.ConnectionStateChanged, onRoomChanged);
            room.off(RoomEvent.ParticipantConnected, onRoomChanged);
            room.off(RoomEvent.ParticipantDisconnected, onRoomChanged);
            room.off(RoomEvent.TrackSubscribed, onRoomChanged);
            room.off(RoomEvent.TrackUnsubscribed, onRoomChanged);
            room.off(RoomEvent.TrackMuted, onRoomChanged);
            room.off(RoomEvent.TrackUnmuted, onRoomChanged);
            room.off(RoomEvent.LocalTrackPublished, onRoomChanged);
            room.off(RoomEvent.LocalTrackUnpublished, onRoomChanged);
            room.off(RoomEvent.Disconnected, onRoomChanged);
            detachRemoteAudioTracks(remoteAudioElementsRef);
            void room.disconnect();
            if (roomRef.current === room) {
                roomRef.current = null;
            }
            syncRoomState();
        };
    }, [
        call.callType,
        call.id,
        call.status,
        callToken?.serverUrl,
        callToken?.token,
        facingMode,
        syncRoomState,
    ]);

    const participantMediaInfos = useMemo<ParticipantMediaInfo[]>(() => {
        const room = roomRef.current;

        return participantInfos.map((participant) => {
            const liveParticipant = participant.isSelf
                ? room?.localParticipant
                : room?.remoteParticipants.get(participant.userId);
            const cameraTrack =
                videoTracks.find(
                    (track) =>
                        track.ownerId === participant.userId &&
                        track.source === Track.Source.Camera,
                ) ?? null;
            const screenTrack =
                videoTracks.find(
                    (track) =>
                        track.ownerId === participant.userId &&
                        track.source === Track.Source.ScreenShare,
                ) ?? null;

            return {
                ...participant,
                isMicrophoneEnabled: participant.isSelf
                    ? isMicrophoneEnabled
                    : Boolean(liveParticipant?.isMicrophoneEnabled),
                isCameraEnabled: participant.isSelf
                    ? isCameraEnabled
                    : Boolean(liveParticipant?.isCameraEnabled || cameraTrack),
                isScreenShareEnabled: participant.isSelf
                    ? isScreenShareEnabled
                    : Boolean(liveParticipant?.isScreenShareEnabled || screenTrack),
                cameraTrack,
                screenTrack,
            };
        });
    }, [
        isCameraEnabled,
        isMicrophoneEnabled,
        isScreenShareEnabled,
        participantInfos,
        videoTracks,
    ]);

    const toggleMicrophone = useCallback(async () => {
        const room = roomRef.current;
        if (!room) return;

        const next = !room.localParticipant.isMicrophoneEnabled;
        try {
            await room.localParticipant.setMicrophoneEnabled(next);
            await playCallSound(next ? "micOn" : "micOff");
            syncRoomState();
        } catch (error) {
            console.error("Failed to toggle microphone", error);
        }
    }, [syncRoomState]);

    const toggleCamera = useCallback(async () => {
        const room = roomRef.current;
        if (!room || !isVideoCall) return;

        const next = !room.localParticipant.isCameraEnabled;
        try {
            await room.localParticipant.setCameraEnabled(next, { facingMode });
            await playCallSound(next ? "cameraOn" : "cameraOff");
            syncRoomState();
        } catch (error) {
            console.error("Failed to toggle camera", error);
        }
    }, [facingMode, isVideoCall, syncRoomState]);

    const switchCamera = useCallback(async () => {
        const room = roomRef.current;
        if (!room || !isVideoCall || !room.localParticipant.isCameraEnabled) return;

        const nextFacingMode = facingMode === "user" ? "environment" : "user";
        try {
            const localTrack = videoTracks.find(
                (track) => track.isLocal && track.source === Track.Source.Camera,
            )?.track as LocalVideoTrack | undefined;

            if (localTrack?.restartTrack) {
                await localTrack.restartTrack({ facingMode: nextFacingMode });
            } else {
                await room.localParticipant.setCameraEnabled(true, {
                    facingMode: nextFacingMode,
                });
            }

            setFacingMode(nextFacingMode);
            await playCallSound("cameraOn");
            syncRoomState();
        } catch (error) {
            console.error("Failed to switch camera", error);
        }
    }, [facingMode, isVideoCall, syncRoomState, videoTracks]);

    const toggleScreenShare = useCallback(async () => {
        const room = roomRef.current;
        if (!room || !isVideoCall || !canShareScreen) return;

        try {
            await room.localParticipant.setScreenShareEnabled(
                !room.localParticipant.isScreenShareEnabled,
            );
            syncRoomState();
        } catch (error) {
            console.error("Failed to toggle screen share", error);
        }
    }, [canShareScreen, isVideoCall, syncRoomState]);

    const connectionError = tokenError || loadError || roomError;
    const isConnecting =
        isTokenLoading ||
        (!!callToken?.token &&
            connectionState !== ConnectionState.Connected &&
            call.status === "Active");

    return (
        <View className="flex-1">
            <View className="flex-1 relative">
                <CallErrorOverlay
                    message={connectionError}
                    isLeaving={isLeaving}
                    onClose={onCloseError}
                />

                <CallStage
                    participants={participantMediaInfos}
                    isVideoCall={isVideoCall && isConnectedToCall}
                    isMobile={isMobile}
                    localFacingMode={facingMode}
                />

                {isConnecting ? <CallLoadingOverlay /> : null}

                {connectionState === ConnectionState.Reconnecting ? (
                    <View className="absolute left-3 top-3 rounded-lg bg-black/60 px-3 py-2">
                        <Text className="text-white text-xs">Восстановление соединения...</Text>
                    </View>
                ) : null}
            </View>

            <CallControls
                micEnabled={isMicrophoneEnabled}
                cameraEnabled={isCameraEnabled}
                screenShareEnabled={isScreenShareEnabled}
                screenShareAvailable={canShareScreen}
                switchCameraAvailable
                callType={call.callType}
                busy={isLeaving}
                isMobile={isMobile}
                onToggleMicrophone={toggleMicrophone}
                onToggleCamera={toggleCamera}
                onSwitchCamera={switchCamera}
                onToggleScreenShare={toggleScreenShare}
                onOpenChat={() => setPanel(panel === "chat" ? "none" : "chat")}
                onOpenParticipants={() =>
                    setPanel(panel === "participants" ? "none" : "participants")
                }
                onLeave={onLeave}
            />

            <CallDrawer
                panel={panel}
                isMobile={isMobile}
                conversationId={conversationId}
                participantInfos={participantInfos}
                onClose={() => setPanel("none")}
            />
        </View>
    );
};
