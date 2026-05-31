import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { apiClient } from "@/shared/lib/api";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { DownloadSimpleIcon, PauseIcon, PlayIcon, WarningCircleIcon } from "@/shared/ui/phosphor";
import { formatVoiceDuration } from "../lib/format";

type VoiceMessagePlayerProps = {
    sourceUri?: string | null;
    mediaAssetId?: string | null;
    durationSeconds?: number | null;
    fileSizeBytes?: number | null;
    isOwn?: boolean;
    compact?: boolean;
};

const WAVEFORM_BARS = [
    8, 10, 8, 14, 18, 12, 22, 28, 18, 12, 16, 24, 30, 18, 12, 20, 26, 16, 10,
    14, 22, 18, 30, 24, 14, 10, 16, 22, 28, 18, 12, 20, 26, 18, 12, 16, 22,
];

const formatFileSize = (size?: number | null): string | null => {
    if (typeof size !== "number" || size <= 0) return null;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    if (size < 1024 * 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
    return `${(size / 1024 / 1024 / 1024).toFixed(1)} GB`;
};

export const VoiceMessagePlayer = ({
    sourceUri,
    mediaAssetId,
    durationSeconds,
    fileSizeBytes,
    isOwn = false,
    compact = false,
}: VoiceMessagePlayerProps) => {
    const audioRef = useRef<HTMLAudioElement | null>(null);
    const [playbackUri, setPlaybackUri] = useState<string | null>(sourceUri ?? null);
    const [isResolving, setIsResolving] = useState(false);
    const [isPlaying, setIsPlaying] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [timelineWidth, setTimelineWidth] = useState(0);
    const [audioDuration, setAudioDuration] = useState(0);
    const [currentSeconds, setCurrentSeconds] = useState(0);

    useEffect(() => {
        const audio = new Audio();
        audio.preload = "metadata";
        audioRef.current = audio;

        const handleLoadedMetadata = () => {
            setAudioDuration(Number.isFinite(audio.duration) ? audio.duration : 0);
        };
        const handleTimeUpdate = () => {
            setCurrentSeconds(audio.currentTime || 0);
        };
        const handleEnded = () => {
            setIsPlaying(false);
            setCurrentSeconds(audio.duration || 0);
        };
        const handlePause = () => setIsPlaying(false);
        const handlePlay = () => setIsPlaying(true);

        audio.addEventListener("loadedmetadata", handleLoadedMetadata);
        audio.addEventListener("timeupdate", handleTimeUpdate);
        audio.addEventListener("ended", handleEnded);
        audio.addEventListener("pause", handlePause);
        audio.addEventListener("play", handlePlay);

        return () => {
            audio.pause();
            audio.removeAttribute("src");
            audio.load();
            audio.removeEventListener("loadedmetadata", handleLoadedMetadata);
            audio.removeEventListener("timeupdate", handleTimeUpdate);
            audio.removeEventListener("ended", handleEnded);
            audio.removeEventListener("pause", handlePause);
            audio.removeEventListener("play", handlePlay);
            audioRef.current = null;
        };
    }, []);

    useEffect(() => {
        if (!sourceUri) return;
        setPlaybackUri(sourceUri);
    }, [sourceUri]);

    const ensurePlaybackUri = useCallback(async () => {
        if (playbackUri) return playbackUri;
        if (!mediaAssetId) throw new Error("Voice message has no media asset id.");

        setIsResolving(true);
        try {
            const { downloadUrl } = await apiClient.media.getAssetDownloadUrl(mediaAssetId);
            setPlaybackUri(downloadUrl);
            return downloadUrl;
        } finally {
            setIsResolving(false);
        }
    }, [mediaAssetId, playbackUri]);

    const totalSeconds = useMemo(() => {
        const metadataDuration = durationSeconds && durationSeconds > 0 ? durationSeconds : 0;
        if (metadataDuration > 0 && audioDuration > 0) {
            return Math.min(metadataDuration, audioDuration);
        }
        return metadataDuration || audioDuration;
    }, [audioDuration, durationSeconds]);
    const safeCurrentSeconds = Math.max(
        0,
        Math.min(currentSeconds, totalSeconds || Number.MAX_SAFE_INTEGER),
    );
    const progress = totalSeconds > 0 ? Math.min(safeCurrentSeconds / totalSeconds, 1) : 0;
    const hasFinished = totalSeconds > 0 && safeCurrentSeconds >= totalSeconds - 0.05 && !isPlaying;
    const isReadyToPlay = Boolean(playbackUri);
    const sizeLabel = useMemo(() => formatFileSize(fileSizeBytes), [fileSizeBytes]);
    const durationLabel =
        totalSeconds > 0 && !isPlaying && safeCurrentSeconds <= 0.05
            ? formatVoiceDuration(totalSeconds)
            : formatVoiceDuration(safeCurrentSeconds);
    const metadataLabel = [durationLabel, sizeLabel].filter(Boolean).join(", ");
    const activeWaveColor = isOwn ? "#FFFFFF" : "#60A5FA";
    const inactiveWaveColor = isOwn ? "rgba(255,255,255,0.32)" : "rgba(255,255,255,0.22)";

    const togglePlayback = useCallback(async () => {
        const audio = audioRef.current;
        if (!audio) return;

        setError(null);
        try {
            if (isPlaying) {
                audio.pause();
                return;
            }

            const uri = await ensurePlaybackUri();
            if (audio.getAttribute("src") !== uri) {
                audio.src = uri;
                audio.load();
            }
            if (hasFinished) {
                audio.currentTime = 0;
                setCurrentSeconds(0);
            }
            await audio.play();
        } catch (playError) {
            console.error("Failed to play voice message", playError);
            setError("Playback failed");
        }
    }, [ensurePlaybackUri, hasFinished, isPlaying]);

    const seekToProgress = useCallback(
        async (nextProgress: number) => {
            const audio = audioRef.current;
            if (!audio || totalSeconds <= 0 || isResolving) return;

            setError(null);
            try {
                const uri = await ensurePlaybackUri();
                if (audio.getAttribute("src") !== uri) {
                    audio.src = uri;
                    audio.load();
                }
                const nextSeconds = Math.max(0, Math.min(nextProgress, 1)) * totalSeconds;
                audio.currentTime = nextSeconds;
                setCurrentSeconds(nextSeconds);
            } catch (seekError) {
                console.error("Failed to seek voice message", seekError);
                setError("Seek failed");
            }
        },
        [ensurePlaybackUri, isResolving, totalSeconds],
    );

    const handleTimelinePress = useCallback(
        (event: any) => {
            if (timelineWidth <= 0) return;
            const locationX = event.nativeEvent?.locationX ?? 0;
            void seekToProgress(locationX / timelineWidth);
        },
        [seekToProgress, timelineWidth],
    );

    const renderWaveformBars = (color: string) =>
        WAVEFORM_BARS.map((height, index) => (
            <View
                key={`${color}-${index}`}
                style={[styles.waveformBar, { height, backgroundColor: color }]}
            />
        ));

    return (
        <View
            testID="voice-message-player"
            className={`flex-row items-center gap-3 ${compact ? "min-w-0" : "min-w-[220px]"}`}
        >
            <Pressable
                testID="voice-message-play-button"
                onPress={togglePlayback}
                disabled={isResolving}
                className={`w-10 h-10 rounded-full items-center justify-center ${
                    isOwn ? "bg-white/20 active:bg-white/30" : "bg-brand-650 active:bg-brand-700"
                } ${isResolving ? "opacity-70" : ""}`}
            >
                {isResolving ? (
                    <ActivityIndicator size="small" className="text-white" />
                ) : isPlaying ? (
                    <PauseIcon size={17} className="text-white" weight="fill" />
                ) : !isReadyToPlay ? (
                    <DownloadSimpleIcon size={17} className="text-white" weight="bold" />
                ) : (
                    <PlayIcon size={17} className="text-white" weight="fill" />
                )}
            </Pressable>

            <View className="flex-1 min-w-0 gap-1">
                <Pressable
                    testID="voice-message-timeline"
                    onPress={handleTimelinePress}
                    disabled={totalSeconds <= 0 || isResolving}
                    onLayout={(event) => setTimelineWidth(event.nativeEvent.layout.width)}
                    className={totalSeconds <= 0 || isResolving ? "opacity-70" : "active:opacity-80"}
                    style={styles.timelineButton}
                >
                    <View style={styles.waveformClip}>
                        <View style={styles.waveformRow}>
                            {renderWaveformBars(inactiveWaveColor)}
                        </View>
                        <View
                            pointerEvents="none"
                            style={[
                                styles.activeWaveformClip,
                                { width: `${Math.max(progress * 100, isPlaying ? 2 : 0)}%` },
                            ]}
                        >
                            <View
                                style={[
                                    styles.waveformRow,
                                    timelineWidth > 0 ? { width: timelineWidth } : null,
                                ]}
                            >
                                {renderWaveformBars(activeWaveColor)}
                            </View>
                        </View>
                    </View>
                </Pressable>
                <View className="flex-row items-center justify-between">
                    <Text
                        className={`text-[11px] font-medium ${
                            isOwn ? "text-white/80" : "text-text-placeholder"
                        }`}
                        numberOfLines={1}
                    >
                        {metadataLabel}
                    </Text>
                </View>
                {error ? (
                    <View className="flex-row items-center gap-1">
                        <WarningCircleIcon size={12} className="text-danger-300" />
                        <Text className="text-[11px] text-danger-300">{error}</Text>
                    </View>
                ) : null}
            </View>
        </View>
    );
};

const styles = StyleSheet.create({
    timelineButton: {
        height: 30,
        justifyContent: "center",
    },
    waveformClip: {
        height: 30,
        justifyContent: "center",
        overflow: "hidden",
    },
    waveformRow: {
        height: 30,
        flexDirection: "row",
        alignItems: "center",
        gap: 2,
    },
    activeWaveformClip: {
        position: "absolute",
        top: 0,
        right: 0,
        bottom: 0,
        left: 0,
        overflow: "hidden",
    },
    waveformBar: {
        width: 2,
        borderRadius: 999,
    },
});
