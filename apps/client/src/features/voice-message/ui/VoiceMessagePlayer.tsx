import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { setAudioModeAsync, useAudioPlayer, useAudioPlayerStatus } from "expo-audio";
import { apiClient } from "@/shared/lib/api";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { PauseIcon, PlayIcon, WarningCircleIcon } from "@/shared/ui/phosphor";
import { formatVoiceDuration } from "../lib/format";

type VoiceMessagePlayerProps = {
    sourceUri?: string | null;
    mediaAssetId?: string | null;
    durationSeconds?: number | null;
    isOwn?: boolean;
    compact?: boolean;
};

const configurePlaybackMode = () =>
    setAudioModeAsync({
        allowsRecording: false,
        playsInSilentMode: true,
        shouldPlayInBackground: false,
        shouldRouteThroughEarpiece: false,
    }).catch(() => undefined);

export const VoiceMessagePlayer = ({
    sourceUri,
    mediaAssetId,
    durationSeconds,
    isOwn = false,
    compact = false,
}: VoiceMessagePlayerProps) => {
    const [playbackUri, setPlaybackUri] = useState<string | null>(sourceUri ?? null);
    const [isResolving, setIsResolving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const loadedUriRef = useRef<string | null>(null);
    const player = useAudioPlayer(null);
    const status = useAudioPlayerStatus(player);

    const replacePlaybackSource = useCallback((uri: string) => {
        if (uri === loadedUriRef.current) return;
        loadedUriRef.current = uri;
        player.replace({ uri });
    }, [player]);

    useEffect(() => {
        if (!sourceUri) return;
        setPlaybackUri(sourceUri);
        replacePlaybackSource(sourceUri);
    }, [replacePlaybackSource, sourceUri]);

    const totalSeconds = useMemo(() => {
        const metadataDuration = durationSeconds && durationSeconds > 0 ? durationSeconds : 0;
        const playerDuration = status.duration && status.duration > 0 ? status.duration : 0;

        if (metadataDuration > 0 && playerDuration > 0) {
            return Math.min(metadataDuration, playerDuration);
        }
        return metadataDuration || playerDuration;
    }, [durationSeconds, status.duration]);
    const currentSeconds = Math.max(0, Math.min(status.currentTime ?? 0, totalSeconds || Number.MAX_SAFE_INTEGER));
    const progress = totalSeconds > 0 ? Math.min(currentSeconds / totalSeconds, 1) : 0;
    const hasFinished = totalSeconds > 0 && currentSeconds >= totalSeconds - 0.05 && !status.playing;

    const ensurePlaybackUri = useCallback(async () => {
        if (playbackUri) {
            replacePlaybackSource(playbackUri);
            return playbackUri;
        }
        if (!mediaAssetId) throw new Error("Voice message has no media asset id.");

        setIsResolving(true);
        try {
            const { downloadUrl } = await apiClient.media.getAssetDownloadUrl(mediaAssetId);
            setPlaybackUri(downloadUrl);
            replacePlaybackSource(downloadUrl);
            return downloadUrl;
        } finally {
            setIsResolving(false);
        }
    }, [mediaAssetId, playbackUri, replacePlaybackSource]);

    const togglePlayback = useCallback(async () => {
        setError(null);
        try {
            if (status.playing) {
                player.pause();
                return;
            }

            await configurePlaybackMode();
            await ensurePlaybackUri();
            if (hasFinished) {
                await player.seekTo(0);
            }
            player.play();
        } catch (playError) {
            console.error("Failed to play voice message", playError);
            setError("Не удалось воспроизвести");
        }
    }, [ensurePlaybackUri, hasFinished, player, status.playing]);

    return (
        <View
            testID="voice-message-player"
            className={`flex-row items-center gap-3 ${compact ? "" : "min-w-[220px]"}`}
        >
            <Pressable
                testID="voice-message-play-button"
                onPress={togglePlayback}
                disabled={isResolving}
                className={`w-9 h-9 rounded-full items-center justify-center ${
                    isOwn ? "bg-white/20 active:bg-white/30" : "bg-brand-650 active:bg-brand-700"
                } ${isResolving ? "opacity-70" : ""}`}
            >
                {isResolving ? (
                    <ActivityIndicator size="small" className="text-white" />
                ) : status.playing ? (
                    <PauseIcon size={17} className="text-white" weight="fill" />
                ) : (
                    <PlayIcon size={17} className="text-white" weight="fill" />
                )}
            </Pressable>

            <View className="flex-1 min-w-0 gap-1.5">
                <View
                    className={isOwn ? "bg-white/25" : "bg-white/15"}
                    style={styles.progressTrack}
                >
                    <View
                        className={isOwn ? "bg-white" : "bg-brand-300"}
                        style={[styles.progressFill, { width: `${Math.max(progress * 100, status.playing ? 3 : 0)}%` }]}
                    />
                </View>
                <View className="flex-row items-center justify-between">
                    <Text className="text-[11px] font-medium text-white">
                        {formatVoiceDuration(currentSeconds)}
                    </Text>
                    <Text className="text-[11px] font-medium text-white">
                        {formatVoiceDuration(totalSeconds)}
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
    progressTrack: {
        height: 4,
        borderRadius: 999,
        overflow: "hidden",
    },
    progressFill: {
        height: 4,
        borderRadius: 999,
    },
});
