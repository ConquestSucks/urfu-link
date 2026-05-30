import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Pressable, Text, View } from "react-native";
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
    SpeakingFrame,
    type CallPanel,
} from "./CallRoomControls";
import {
    pickActiveScreenShare,
    shouldDisableLocalScreenShareForRemoteOwner,
    type ScreenShareCandidate,
} from "./CallRoomMedia";

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
    isSpeaking: boolean;
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
    objectFit = "cover",
}: {
    track: LocalVideoTrack | RemoteVideoTrack;
    muted?: boolean;
    mirrored?: boolean;
    objectFit?: "cover" | "contain";
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
            objectFit,
            transform: mirrored ? "scaleX(-1)" : undefined,
        },
    });
};

const ParticipantTile = ({
    participant,
    isVideoCall,
    localFacingMode,
    compact = false,
}: {
    participant: ParticipantMediaInfo;
    isVideoCall: boolean;
    localFacingMode: CameraFacingMode;
    compact?: boolean;
}) => {
    const visibleTrack = participant.cameraTrack;
    const shouldMirror =
        participant.isSelf &&
        visibleTrack === participant.cameraTrack &&
        localFacingMode === "user";

    return (
        <SpeakingFrame isSpeaking={participant.isSpeaking}>
            <View testID="call-participant-tile" className="flex-1 min-h-0">
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
                                size={compact ? 48 : 96}
                                src={participant.avatarUrl}
                                name={participant.displayName}
                            />
                            <Text className={`${compact ? "text-xs" : "text-base"} text-white font-semibold`}>
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
                                <Text className="text-white/80 text-[11px]">
                                    Камера выключена
                                </Text>
                            </View>
                        ) : null}
                        {participant.isScreenShareEnabled ? (
                            <View className="rounded-full bg-brand-600/90 px-2 py-1">
                                <Text className="text-white text-[11px]">
                                    Демонстрация экрана
                                </Text>
                            </View>
                        ) : null}
                    </View>
                </View>
            </View>
        </SpeakingFrame>
    );
};

const CallStage = ({
    participants,
    isVideoCall,
    isMobile,
    localFacingMode,
    activeScreenShare,
    onOpenScreenShareFullscreen,
}: {
    participants: ParticipantMediaInfo[];
    isVideoCall: boolean;
    isMobile: boolean;
    localFacingMode: CameraFacingMode;
    activeScreenShare: WebVideoTrackRef | null;
    onOpenScreenShareFullscreen: () => void;
}) => {
    const screenOwner = activeScreenShare
        ? participants.find((participant) => participant.userId === activeScreenShare.ownerId) ?? null
        : null;

    if (activeScreenShare && screenOwner) {
        return (
            <View className={`flex-1 gap-3 p-3 ${isMobile ? "" : "flex-row"}`}>
                <View className="flex-1 rounded-[28px] overflow-hidden border border-white/10 bg-black">
                    <View className="flex-1">
                        <VideoElement
                            track={activeScreenShare.track}
                            muted={activeScreenShare.isLocal}
                            objectFit="contain"
                        />
                        <View className="absolute left-4 top-4 rounded-full bg-black/70 px-3 py-1.5">
                            <Text className="text-white text-xs font-semibold">
                                {screenOwner.displayName} показывает экран
                            </Text>
                        </View>
                        <Pressable
                            testID="call-screen-fullscreen"
                            accessibilityLabel="Открыть демонстрацию на весь экран"
                            onPress={onOpenScreenShareFullscreen}
                            className="absolute right-4 top-4 rounded-full bg-white/12 px-3 py-2"
                        >
                            <Text className="text-white text-xs font-semibold">Во весь экран</Text>
                        </Pressable>
                    </View>
                </View>
                <ParticipantRail
                    participants={participants}
                    isVideoCall={isVideoCall}
                    isMobile={isMobile}
                    localFacingMode={localFacingMode}
                />
            </View>
        );
    }

    if (!isMobile && participants.length > 2) {
        const primary =
            participants.find((participant) => participant.isSpeaking) ??
            participants.find((participant) => participant.cameraTrack) ??
            participants[0];
        const railParticipants = participants.filter(
            (participant) => participant.userId !== primary.userId,
        );

        return (
            <View className="flex-1 flex-row gap-3 p-3">
                <ParticipantTile
                    participant={primary}
                    isVideoCall={isVideoCall}
                    localFacingMode={localFacingMode}
                />
                <ParticipantRail
                    participants={railParticipants}
                    isVideoCall={isVideoCall}
                    isMobile={isMobile}
                    localFacingMode={localFacingMode}
                />
            </View>
        );
    }

    return (
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
};

const ParticipantRail = ({
    participants,
    isVideoCall,
    isMobile,
    localFacingMode,
}: {
    participants: ParticipantMediaInfo[];
    isVideoCall: boolean;
    isMobile: boolean;
    localFacingMode: CameraFacingMode;
}) => {
    const visibleParticipants = participants.slice(0, isMobile ? 4 : 6);
    const overflow = Math.max(0, participants.length - visibleParticipants.length);

    return (
        <View
            className={`gap-2 ${isMobile ? "flex-row h-28" : "w-24"}`}
            testID="call-participant-rail"
        >
            {visibleParticipants.map((participant) => (
                <View
                    key={participant.userId}
                    className={`${isMobile ? "w-24" : "h-20"} rounded-2xl overflow-hidden`}
                >
                    <ParticipantTile
                        participant={participant}
                        isVideoCall={isVideoCall}
                        localFacingMode={localFacingMode}
                        compact
                    />
                </View>
            ))}
            {overflow > 0 ? (
                <View className={`${isMobile ? "w-20" : "h-14"} rounded-2xl bg-white/10 items-center justify-center`}>
                    <Text className="text-white text-sm font-semibold">+{overflow}</Text>
                </View>
            ) : null}
        </View>
    );
};

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
    const [activeSpeakerIds, setActiveSpeakerIds] = useState<Set<string>>(() => new Set());
    const [activeScreenShareOwnerId, setActiveScreenShareOwnerId] = useState<string | null>(null);
    const [isScreenShareFullscreen, setIsScreenShareFullscreen] = useState(false);
    const [facingMode, setFacingMode] = useState<CameraFacingMode>("user");
    const [videoInputCount, setVideoInputCount] = useState(0);
    const knownRemoteScreenShareKeysRef = useRef<Set<string>>(new Set());
    const browserFullscreenActiveRef = useRef(false);

    const isVideoCall = call.callType === "Video";
    const isConnectedToCall = call.status === "Active";
    const canShareScreen = useMemo(
        () =>
            typeof navigator !== "undefined" &&
            Boolean(navigator.mediaDevices?.getDisplayMedia),
        [],
    );
    const canSwitchCamera = isMobile && videoInputCount > 1;
    const localUserId = participantInfos.find((participant) => participant.isSelf)?.userId ?? null;

    const refreshVideoInputs = useCallback(async () => {
        try {
            if (
                typeof navigator === "undefined" ||
                !navigator.mediaDevices?.enumerateDevices
            ) {
                setVideoInputCount(0);
                return;
            }

            const devices = await navigator.mediaDevices?.enumerateDevices?.();
            setVideoInputCount(
                devices?.filter((device) => device.kind === "videoinput").length ?? 0,
            );
        } catch (error) {
            console.warn("Failed to enumerate video inputs", error);
            setVideoInputCount(0);
        }
    }, []);

    useEffect(() => {
        void refreshVideoInputs();

        if (typeof navigator === "undefined") {
            return undefined;
        }

        const mediaDevices = navigator.mediaDevices;
        mediaDevices?.addEventListener?.("devicechange", refreshVideoInputs);

        return () => {
            mediaDevices?.removeEventListener?.("devicechange", refreshVideoInputs);
        };
    }, [refreshVideoInputs]);

    const syncRoomState = useCallback((syncActiveSpeakers = true) => {
        const room = roomRef.current;
        if (!room) {
            setConnectionState(ConnectionState.Disconnected);
            setIsMicrophoneEnabled(false);
            setIsCameraEnabled(false);
            setIsScreenShareEnabled(false);
            setVideoTracks([]);
            setActiveSpeakerIds(new Set());
            return;
        }

        setConnectionState(room.state);
        setIsMicrophoneEnabled(room.localParticipant.isMicrophoneEnabled);
        setIsCameraEnabled(room.localParticipant.isCameraEnabled);
        setIsScreenShareEnabled(room.localParticipant.isScreenShareEnabled);
        setVideoTracks(collectVideoTracks(room, participantInfos));
        if (syncActiveSpeakers) {
            setActiveSpeakerIds(
                new Set(room.activeSpeakers.map((speaker) => speaker.identity)),
            );
        }
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
        const onActiveSpeakersChanged = (
            speakers: Array<{ identity: string }>,
        ) => {
            if (isDisposed) return;

            setActiveSpeakerIds(new Set(speakers.map((speaker) => speaker.identity)));
            syncRoomState(false);
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
        room.on(RoomEvent.ActiveSpeakersChanged, onActiveSpeakersChanged);
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
                    void refreshVideoInputs();
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
            room.off(RoomEvent.ActiveSpeakersChanged, onActiveSpeakersChanged);
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
        refreshVideoInputs,
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
                isSpeaking: activeSpeakerIds.has(participant.userId) || Boolean(liveParticipant?.isSpeaking),
                cameraTrack,
                screenTrack,
            };
        });
    }, [
        activeSpeakerIds,
        isCameraEnabled,
        isMicrophoneEnabled,
        isScreenShareEnabled,
        participantInfos,
        videoTracks,
    ]);

    const screenShareCandidates = useMemo<ScreenShareCandidate[]>(
        () =>
            videoTracks
                .filter((track) => track.source === Track.Source.ScreenShare)
                .map((track) => ({
                    ownerId: track.ownerId,
                    key: track.key,
                    isLocal: track.isLocal,
                })),
        [videoTracks],
    );

    const activeScreenShareCandidate = useMemo(
        () =>
            pickActiveScreenShare(
                screenShareCandidates,
                activeScreenShareOwnerId,
                localUserId,
            ),
        [activeScreenShareOwnerId, localUserId, screenShareCandidates],
    );

    const activeScreenShareTrack = useMemo(
        () =>
            activeScreenShareCandidate
                ? videoTracks.find((track) => track.key === activeScreenShareCandidate.key) ?? null
                : null,
        [activeScreenShareCandidate, videoTracks],
    );

    useEffect(() => {
        const nextOwnerId = activeScreenShareCandidate?.ownerId ?? null;
        if (nextOwnerId !== activeScreenShareOwnerId) {
            setActiveScreenShareOwnerId(nextOwnerId);
        }
        if (!nextOwnerId) {
            setIsScreenShareFullscreen(false);
        }
    }, [activeScreenShareCandidate?.ownerId, activeScreenShareOwnerId]);

    useEffect(() => {
        const remoteCandidates = screenShareCandidates.filter((candidate) => !candidate.isLocal);
        const knownRemoteKeys = knownRemoteScreenShareKeysRef.current;
        const latestNewRemote = [...remoteCandidates]
            .reverse()
            .find((candidate) => !knownRemoteKeys.has(candidate.key));

        knownRemoteScreenShareKeysRef.current = new Set(
            remoteCandidates.map((candidate) => candidate.key),
        );

        if (
            latestNewRemote &&
            shouldDisableLocalScreenShareForRemoteOwner(
                localUserId,
                latestNewRemote.ownerId,
                isScreenShareEnabled,
            )
        ) {
            setActiveScreenShareOwnerId(latestNewRemote.ownerId);
            void roomRef.current?.localParticipant
                .setScreenShareEnabled(false)
                .catch((error) => {
                    console.error("Failed to stop local screen share after remote owner changed", error);
                });
        }
    }, [isScreenShareEnabled, localUserId, screenShareCandidates]);

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
        if (!room || !isConnectedToCall) return;

        const next = !room.localParticipant.isCameraEnabled;
        try {
            await room.localParticipant.setCameraEnabled(next, { facingMode });
            await playCallSound(next ? "cameraOn" : "cameraOff");
            if (next) {
                void refreshVideoInputs();
            }
            syncRoomState();
        } catch (error) {
            console.error("Failed to toggle camera", error);
        }
    }, [facingMode, isConnectedToCall, refreshVideoInputs, syncRoomState]);

    const switchCamera = useCallback(async () => {
        const room = roomRef.current;
        if (
            !room ||
            !isConnectedToCall ||
            !room.localParticipant.isCameraEnabled ||
            !canSwitchCamera
        ) {
            return;
        }

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
    }, [canSwitchCamera, facingMode, isConnectedToCall, syncRoomState, videoTracks]);

    const toggleScreenShare = useCallback(async () => {
        const room = roomRef.current;
        if (!room || !isConnectedToCall || !canShareScreen) return;

        try {
            const next = !room.localParticipant.isScreenShareEnabled;
            await room.localParticipant.setScreenShareEnabled(
                next,
            );
            if (next) {
                setActiveScreenShareOwnerId(localUserId);
            } else if (activeScreenShareOwnerId === localUserId) {
                setActiveScreenShareOwnerId(null);
                setIsScreenShareFullscreen(false);
            }
            syncRoomState();
        } catch (error) {
            console.error("Failed to toggle screen share", error);
        }
    }, [activeScreenShareOwnerId, canShareScreen, isConnectedToCall, localUserId, syncRoomState]);

    const connectionError = tokenError || loadError || roomError;
    const isConnecting =
        isTokenLoading ||
        (!!callToken?.token &&
            connectionState !== ConnectionState.Connected &&
            call.status === "Active");

    const openScreenShareFullscreen = useCallback(() => {
        if (!activeScreenShareTrack) return;

        setPanel("none");
        setIsScreenShareFullscreen(true);
        const requestFullscreen = document.documentElement.requestFullscreen;
        if (requestFullscreen) {
            void requestFullscreen.call(document.documentElement)
                .then(() => {
                    browserFullscreenActiveRef.current = true;
                })
                .catch(() => {
                    browserFullscreenActiveRef.current = false;
                });
        }
    }, [activeScreenShareTrack]);

    const closeScreenShareFullscreen = useCallback(() => {
        setIsScreenShareFullscreen(false);
        const shouldExitBrowserFullscreen =
            browserFullscreenActiveRef.current && document.fullscreenElement;
        browserFullscreenActiveRef.current = false;
        if (shouldExitBrowserFullscreen) {
            void document.exitFullscreen?.().catch(() => undefined);
        }
    }, []);

    useEffect(() => {
        if (!isScreenShareFullscreen) return undefined;

        const onKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") {
                closeScreenShareFullscreen();
            }
        };
        const onFullscreenChange = () => {
            if (browserFullscreenActiveRef.current && !document.fullscreenElement) {
                browserFullscreenActiveRef.current = false;
                setIsScreenShareFullscreen(false);
            }
        };

        window.addEventListener("keydown", onKeyDown);
        document.addEventListener("fullscreenchange", onFullscreenChange);

        return () => {
            window.removeEventListener("keydown", onKeyDown);
            document.removeEventListener("fullscreenchange", onFullscreenChange);
        };
    }, [closeScreenShareFullscreen, isScreenShareFullscreen]);

    useEffect(() => {
        if (!activeScreenShareTrack && isScreenShareFullscreen) {
            closeScreenShareFullscreen();
        }
    }, [activeScreenShareTrack, closeScreenShareFullscreen, isScreenShareFullscreen]);

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
                    isVideoCall={isConnectedToCall}
                    isMobile={isMobile}
                    localFacingMode={facingMode}
                    activeScreenShare={activeScreenShareTrack}
                    onOpenScreenShareFullscreen={openScreenShareFullscreen}
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
                switchCameraAvailable={canSwitchCamera}
                screenShareAvailable={canShareScreen}
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

            {isScreenShareFullscreen && activeScreenShareTrack ? (
                <View className="absolute inset-0 z-50 bg-black">
                    <VideoElement
                        track={activeScreenShareTrack.track}
                        muted={activeScreenShareTrack.isLocal}
                        objectFit="contain"
                    />
                    <Pressable
                        accessibilityLabel="Закрыть полноэкранную демонстрацию"
                        onPress={closeScreenShareFullscreen}
                        className="absolute right-4 top-4 rounded-full bg-white/15 px-4 py-2"
                    >
                        <Text className="text-white text-sm font-semibold">Закрыть</Text>
                    </Pressable>
                </View>
            ) : null}
        </View>
    );
};
