import { useCallback, useMemo, useState } from "react";
import { View } from "react-native";
import {
    LiveKitRoom,
    useLocalParticipant,
    useTracks,
    VideoTrack,
} from "@livekit/react-native";
import { Track } from "livekit-client";
import type { CallRoomProps } from "./CallRoom.types";
import {
    CallControls,
    CallDrawer,
    CallErrorOverlay,
    CallLoadingOverlay,
    type CallPanel,
    NoVideoPanel,
} from "./CallRoomControls";

type TrackRef = Parameters<typeof VideoTrack>[0]["trackRef"];
type DefinedTrackRef = Exclude<TrackRef, null | undefined>;

const NativeVideoPanel = ({
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
        return <NoVideoPanel label={mainPlaceholderLabel} />;
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

const NativeRoomContent = ({
    call,
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
    const [panel, setPanel] = useState<CallPanel>("none");
    const isVideoCall = call.callType === "Video";
    const isConnectedToCall = call.status === "Active";

    const { isMicrophoneEnabled, isCameraEnabled, isScreenShareEnabled, localParticipant } =
        useLocalParticipant();
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
        // Native screen share is out of scope for the current mobile implementation.
    }, []);

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
                        isVideoAvailable={isVideoCall && isConnectedToCall}
                        mainTrack={mainCameraTrack}
                        thumbnailTracks={thumbnailCameraTracks}
                        mainPlaceholderLabel={callTitle}
                    />
                )}
            </View>

            <CallControls
                micEnabled={isMicrophoneEnabled}
                cameraEnabled={isCameraEnabled}
                screenShareEnabled={isScreenShareEnabled}
                screenShareAvailable={false}
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
            video={isVideoCall && (isConnectedToCall || call.status === "Ringing")}
            screen={false}
            connect={Boolean(callToken?.token)}
            onDisconnected={() => {
                // keep room auto-disconnecting on leave.
            }}
        >
            <NativeRoomContent {...props} />
        </LiveKitRoom>
    );
};
