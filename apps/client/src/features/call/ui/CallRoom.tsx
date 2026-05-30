import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Pressable, View } from "react-native";
import {
    LiveKitRoom,
    useLocalParticipant,
    useTracks,
    VideoTrack,
} from "@livekit/react-native";
import { Track } from "livekit-client";
import { playCallSound } from "@/shared/lib/call-sounds";
import type { CallRoomProps } from "./CallRoom.types";
import {
    CallControls,
    CallDrawer,
    CallErrorOverlay,
    CallLoadingOverlay,
    type CallPanel,
    NoVideoPanel,
} from "./CallRoomControls";
import {
    pickActiveScreenShare,
    SCREEN_SHARE_CAPTURE_OPTIONS,
    shouldDisableLocalScreenShareForRemoteOwner,
    type ScreenShareCandidate,
} from "./CallRoomMedia";

type TrackRef = Parameters<typeof VideoTrack>[0]["trackRef"];
type DefinedTrackRef = Exclude<TrackRef, null | undefined>;
type CameraFacingMode = "user" | "environment";

const getNativeTrackKey = (track: DefinedTrackRef) =>
    `${track.source}:${track.participant.identity}:${track.publication?.trackSid ?? track.source}`;

const getNativeScreenShareKey = (track: DefinedTrackRef) =>
    `${track.participant.identity}:${track.publication?.trackSid ?? track.source}`;

const NativeVideoPanel = ({
    isVideoAvailable,
    mainTrack,
    thumbnailTracks,
    mainPlaceholderLabel,
    onSelectTrack,
}: {
    isVideoAvailable: boolean;
    mainTrack: DefinedTrackRef | null;
    thumbnailTracks: DefinedTrackRef[];
    mainPlaceholderLabel: string;
    onSelectTrack: (track: DefinedTrackRef) => void;
}) => {
    return (
        <View className="relative flex-1 bg-black/30">
            {!isVideoAvailable || !mainTrack ? (
                <NoVideoPanel label={mainPlaceholderLabel} />
            ) : (
                <VideoTrack
                    trackRef={mainTrack}
                    style={{
                        width: "100%",
                        height: "100%",
                    }}
                />
            )}

            {isVideoAvailable && mainTrack && thumbnailTracks.length > 0 ? (
                <View className="absolute top-3 right-3 w-48 gap-2">
                    {thumbnailTracks.map((thumbnailTrack) => (
                        <Pressable
                            key={`${thumbnailTrack.publication?.trackSid ?? "thumb"}-${thumbnailTrack.source}`}
                            onPress={() => onSelectTrack(thumbnailTrack)}
                            className="w-full h-28 rounded-lg overflow-hidden border border-white/20"
                        >
                            <VideoTrack trackRef={thumbnailTrack} />
                        </Pressable>
                    ))}
                </View>
            ) : null}

        </View>
    );
};

const NativeRoomContent = ({
    call,
    isTokenLoading,
    tokenError,
    loadError,
    isLeaving,
    isMobile,
    callTitle,
    conversationId,
    participantInfos,
    onLeave,
    onCloseError,
}: CallRoomProps) => {
    const [panel, setPanel] = useState<CallPanel>("none");
    const [facingMode, setFacingMode] = useState<CameraFacingMode>("user");
    const [activeScreenShareOwnerId, setActiveScreenShareOwnerId] = useState<string | null>(null);
    const [selectedStageTrackKey, setSelectedStageTrackKey] = useState<string | null>(null);
    const isVideoCall = call.callType === "Video";
    const isConnectedToCall = call.status === "Active";

    const { isMicrophoneEnabled, isCameraEnabled, isScreenShareEnabled, localParticipant } =
        useLocalParticipant();
    const cameraTracks = useTracks([Track.Source.Camera], {
        onlySubscribed: true,
    });
    const screenShareTracks = useTracks([Track.Source.ScreenShare], {
        onlySubscribed: true,
    });
    const knownRemoteScreenShareKeysRef = useRef<Set<string>>(new Set());
    const localUserId =
        participantInfos.find((participant) => participant.isSelf)?.userId ??
        localParticipant?.identity ??
        null;

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
    const allScreenShareTracks = useMemo<DefinedTrackRef[]>(
        () =>
            screenShareTracks.filter(
                (track): track is DefinedTrackRef =>
                    track !== null && track !== undefined,
            ),
        [screenShareTracks],
    );
    const remoteScreenShareTracks = useMemo<DefinedTrackRef[]>(
        () => allScreenShareTracks.filter((track) => !track.participant.isLocal),
        [allScreenShareTracks],
    );
    const screenShareCandidates = useMemo<ScreenShareCandidate[]>(
        () =>
            allScreenShareTracks.map((track) => ({
                ownerId: track.participant.identity,
                key: getNativeScreenShareKey(track),
                isLocal: track.participant.isLocal,
            })),
        [allScreenShareTracks],
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
                ? allScreenShareTracks.find(
                    (track) => getNativeScreenShareKey(track) === activeScreenShareCandidate.key,
                ) ?? null
                : null,
        [activeScreenShareCandidate, allScreenShareTracks],
    );

    useEffect(() => {
        const nextOwnerId = activeScreenShareCandidate?.ownerId ?? null;
        if (nextOwnerId !== activeScreenShareOwnerId) {
            setActiveScreenShareOwnerId(nextOwnerId);
        }
    }, [activeScreenShareCandidate?.ownerId, activeScreenShareOwnerId]);

    useEffect(() => {
        const knownRemoteKeys = knownRemoteScreenShareKeysRef.current;
        const latestNewRemote = [...remoteScreenShareTracks]
            .reverse()
            .find((track) => !knownRemoteKeys.has(getNativeScreenShareKey(track)));

        knownRemoteScreenShareKeysRef.current = new Set(
            remoteScreenShareTracks.map(getNativeScreenShareKey),
        );

        if (
            latestNewRemote &&
            localParticipant &&
            shouldDisableLocalScreenShareForRemoteOwner(
                localUserId,
                latestNewRemote.participant.identity,
                isScreenShareEnabled,
            )
        ) {
            setActiveScreenShareOwnerId(latestNewRemote.participant.identity);
            void localParticipant.setScreenShareEnabled(false).catch((error) => {
                console.error("Failed to stop local screen share after remote owner changed", error);
            });
        }
    }, [isScreenShareEnabled, localParticipant, localUserId, remoteScreenShareTracks]);

    const stageTracks = useMemo(
        () => [
            ...(activeScreenShareTrack ? [activeScreenShareTrack] : []),
            ...remoteCameraTracks,
            ...(localCameraTrack ? [localCameraTrack] : []),
        ],
        [activeScreenShareTrack, localCameraTrack, remoteCameraTracks],
    );

    const mainCameraTrack = useMemo(
        () =>
            (selectedStageTrackKey
                ? stageTracks.find((track) => getNativeTrackKey(track) === selectedStageTrackKey)
                : null) ??
            activeScreenShareTrack ??
            remoteCameraTracks[0] ??
            localCameraTrack,
        [
            activeScreenShareTrack,
            localCameraTrack,
            remoteCameraTracks,
            selectedStageTrackKey,
            stageTracks,
        ],
    );

    useEffect(() => {
        if (
            selectedStageTrackKey &&
            !stageTracks.some((track) => getNativeTrackKey(track) === selectedStageTrackKey)
        ) {
            setSelectedStageTrackKey(null);
        }
    }, [selectedStageTrackKey, stageTracks]);

    const thumbnailCameraTracks = useMemo(
        () =>
            mainCameraTrack
                ? stageTracks.filter(
                    (track) => getNativeTrackKey(track) !== getNativeTrackKey(mainCameraTrack),
                )
                : [],
        [mainCameraTrack, stageTracks],
    );

    const toggleMicrophone = useCallback(async () => {
        if (!localParticipant) return;
        try {
            const next = !isMicrophoneEnabled;
            await localParticipant.setMicrophoneEnabled(next);
            await playCallSound(next ? "micOn" : "micOff");
        } catch (error) {
            console.error("Failed to toggle microphone", error);
        }
    }, [isMicrophoneEnabled, localParticipant]);

    const toggleCamera = useCallback(async () => {
        if (!localParticipant || !isConnectedToCall) return;
        try {
            const next = !isCameraEnabled;
            await localParticipant.setCameraEnabled(next, { facingMode });
        } catch (error) {
            console.error("Failed to toggle camera", error);
        }
    }, [facingMode, isConnectedToCall, isCameraEnabled, localParticipant]);

    const switchCamera = useCallback(async () => {
        if (!localParticipant || !isConnectedToCall || !isCameraEnabled) return;

        const nextFacingMode: CameraFacingMode = facingMode === "user" ? "environment" : "user";
        try {
            const localTrack = localCameraTrack?.publication?.track as
                | { restartTrack?: (options?: { facingMode?: CameraFacingMode }) => Promise<void> }
                | undefined;

            if (localTrack?.restartTrack) {
                await localTrack.restartTrack({ facingMode: nextFacingMode });
            } else {
                await localParticipant.setCameraEnabled(true, { facingMode: nextFacingMode });
            }

            setFacingMode(nextFacingMode);
        } catch (error) {
            console.error("Failed to switch camera", error);
        }
    }, [facingMode, isCameraEnabled, isConnectedToCall, localCameraTrack, localParticipant]);

    const toggleScreenShare = useCallback(async () => {
        if (!localParticipant || !isConnectedToCall) return;

        try {
            const next = !isScreenShareEnabled;
            await localParticipant.setScreenShareEnabled(
                next,
                next ? SCREEN_SHARE_CAPTURE_OPTIONS : undefined,
            );
            if (next) {
                setActiveScreenShareOwnerId(localUserId);
            } else if (activeScreenShareOwnerId === localUserId) {
                setActiveScreenShareOwnerId(null);
            }
        } catch (error) {
            console.error("Failed to toggle screen share", error);
        }
    }, [
        activeScreenShareOwnerId,
        isConnectedToCall,
        isScreenShareEnabled,
        localParticipant,
        localUserId,
    ]);

    return (
        <View className="flex-1">
            <View className="flex-1 relative">
                <CallErrorOverlay
                    message={tokenError || loadError}
                    isLeaving={isLeaving}
                    onClose={onCloseError}
                />

                {isTokenLoading ? (
                    <CallLoadingOverlay />
                ) : (
                    <NativeVideoPanel
                        isVideoAvailable={isConnectedToCall}
                        mainTrack={mainCameraTrack}
                        thumbnailTracks={thumbnailCameraTracks}
                        mainPlaceholderLabel={callTitle}
                        onSelectTrack={(track) => setSelectedStageTrackKey(getNativeTrackKey(track))}
                    />
                )}
            </View>

            <CallControls
                micEnabled={isMicrophoneEnabled}
                cameraEnabled={isCameraEnabled}
                screenShareEnabled={isScreenShareEnabled}
                switchCameraAvailable={isCameraEnabled}
                screenShareAvailable={isConnectedToCall}
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

export const CallRoom = (props: CallRoomProps) => {
    const { call, callToken } = props;
    const isVideoCall = call.callType === "Video";
    const isConnectedToCall = call.status === "Active";

    return (
        <LiveKitRoom
            key={call.id}
            serverUrl={callToken?.serverUrl}
            token={callToken?.token}
            audio={true}
            video={isVideoCall && isConnectedToCall}
            screen={false}
            connect={Boolean(isConnectedToCall && callToken?.token)}
            onDisconnected={() => {
                // keep room auto-disconnecting on leave.
            }}
        >
            <NativeRoomContent {...props} />
        </LiveKitRoom>
    );
};
