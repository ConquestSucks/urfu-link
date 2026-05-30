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
import type { CallRoomProps } from "./CallRoom.types";
import {
    CallControls,
    CallDrawer,
    CallErrorOverlay,
    CallLoadingOverlay,
    type CallPanel,
    NoVideoPanel,
} from "./CallRoomControls";

type WebVideoTrackRef = {
    key: string;
    track: LocalVideoTrack | RemoteVideoTrack;
};

const collectVideoTracks = (room: Room) => {
    const remoteCameraTracks: WebVideoTrackRef[] = [];

    room.remoteParticipants.forEach((participant) => {
        participant.videoTrackPublications.forEach((publication) => {
            const track = publication.videoTrack;
            if (publication.source !== Track.Source.Camera || !track || publication.isMuted) {
                return;
            }

            remoteCameraTracks.push({
                key: `${participant.sid}-${publication.trackSid}`,
                track,
            });
        });
    });

    let localCameraTrack: WebVideoTrackRef | null = null;
    room.localParticipant.videoTrackPublications.forEach((publication) => {
        const track = publication.videoTrack;
        if (publication.source !== Track.Source.Camera || !track || publication.isMuted) {
            return;
        }

        localCameraTrack = {
            key: `local-${publication.trackSid}`,
            track,
        };
    });

    return { localCameraTrack, remoteCameraTracks };
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
}: {
    track: LocalVideoTrack | RemoteVideoTrack;
    muted?: boolean;
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
        },
    });
};

const WebVideoPanel = ({
    isVideoAvailable,
    mainTrack,
    thumbnailTracks,
    mainPlaceholderLabel,
}: {
    isVideoAvailable: boolean;
    mainTrack: WebVideoTrackRef | null;
    thumbnailTracks: WebVideoTrackRef[];
    mainPlaceholderLabel: string;
}) => {
    if (!isVideoAvailable || !mainTrack) {
        return <NoVideoPanel label={mainPlaceholderLabel} />;
    }

    return (
        <View className="relative flex-1 bg-black/30">
            <VideoElement track={mainTrack.track} />

            {thumbnailTracks.length > 0 ? (
                <View className="absolute top-3 right-3 w-48 gap-2">
                    {thumbnailTracks.map((thumbnailTrack) => (
                        <View
                            key={thumbnailTrack.key}
                            className="w-full h-28 rounded-lg overflow-hidden border border-white/20"
                        >
                            <VideoElement track={thumbnailTrack.track} muted />
                        </View>
                    ))}
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
    callTitle,
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
    const [localCameraTrack, setLocalCameraTrack] = useState<WebVideoTrackRef | null>(null);
    const [remoteCameraTracks, setRemoteCameraTracks] = useState<WebVideoTrackRef[]>([]);

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
            setLocalCameraTrack(null);
            setRemoteCameraTracks([]);
            return;
        }

        const tracks = collectVideoTracks(room);
        setConnectionState(room.state);
        setIsMicrophoneEnabled(room.localParticipant.isMicrophoneEnabled);
        setIsCameraEnabled(room.localParticipant.isCameraEnabled);
        setIsScreenShareEnabled(room.localParticipant.isScreenShareEnabled);
        setLocalCameraTrack(tracks.localCameraTrack);
        setRemoteCameraTracks(tracks.remoteCameraTracks);
        syncRemoteAudioTracks(room, remoteAudioElementsRef);
    }, []);

    useEffect(() => {
        if (!callToken?.serverUrl || !callToken.token || call.status === "Ended") {
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
                    await room.localParticipant.setCameraEnabled(true).catch((error) => {
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
        syncRoomState,
    ]);

    const toggleMicrophone = useCallback(async () => {
        const room = roomRef.current;
        if (!room) return;

        try {
            await room.localParticipant.setMicrophoneEnabled(
                !room.localParticipant.isMicrophoneEnabled,
            );
            syncRoomState();
        } catch (error) {
            console.error("Failed to toggle microphone", error);
        }
    }, [syncRoomState]);

    const toggleCamera = useCallback(async () => {
        const room = roomRef.current;
        if (!room || !isVideoCall) return;

        try {
            await room.localParticipant.setCameraEnabled(!room.localParticipant.isCameraEnabled);
            syncRoomState();
        } catch (error) {
            console.error("Failed to toggle camera", error);
        }
    }, [isVideoCall, syncRoomState]);

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

    const mainCameraTrack = remoteCameraTracks[0] ?? localCameraTrack;
    const thumbnailCameraTracks = useMemo(() => {
        if (!mainCameraTrack) {
            return [];
        }

        if (mainCameraTrack === localCameraTrack) {
            return remoteCameraTracks;
        }

        return localCameraTrack ? [...remoteCameraTracks.slice(1), localCameraTrack] : remoteCameraTracks.slice(1);
    }, [remoteCameraTracks, localCameraTrack, mainCameraTrack]);

    const connectionError =
        tokenError || loadError || roomError;
    const isConnecting =
        isTokenLoading ||
        (!!callToken?.token &&
            connectionState !== ConnectionState.Connected &&
            call.status !== "Ended");

    return (
        <View className="flex-1">
            <View className="flex-1 relative">
                <CallErrorOverlay
                    message={connectionError}
                    isLeaving={isLeaving}
                    onClose={onCloseError}
                />

                {isConnecting ? (
                    <CallLoadingOverlay />
                ) : (
                    <WebVideoPanel
                        isVideoAvailable={isVideoCall && isConnectedToCall}
                        mainTrack={mainCameraTrack}
                        thumbnailTracks={thumbnailCameraTracks}
                        mainPlaceholderLabel={callTitle}
                    />
                )}

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
                callType={call.callType}
                busy={isLeaving}
                onToggleMicrophone={toggleMicrophone}
                onToggleCamera={toggleCamera}
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
                participantInfos={participantInfos}
                onClose={() => setPanel("none")}
            />
        </View>
    );
};
