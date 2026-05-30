import React, { useCallback, useEffect, useMemo, useState } from "react";
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
    const player = useAudioPlayer(playbackUri ? { uri: playbackUri } : null);
    const status = useAudioPlayerStatus(player);

    useEffect(() => {
        if (!sourceUri || sourceUri === playbackUri) return;
        setPlaybackUri(sourceUri);
        player.replace({ uri: sourceUri });
    }, [player, playbackUri, sourceUri]);

    const totalSeconds = useMemo(() => {
        if (durationSeconds && durationSeconds > 0) return durationSeconds;
        return status.duration && status.duration > 0 ? status.duration : 0;
    }, [durationSeconds, status.duration]);
    const currentSeconds = Math.max(0, Math.min(status.currentTime ?? 0, totalSeconds || Number.MAX_SAFE_INTEGER));
    const progress = totalSeconds > 0 ? Math.min(currentSeconds / totalSeconds, 1) : 0;
    const hasFinished = totalSeconds > 0 && currentSeconds >= totalSeconds - 0.05 && !status.playing;

    const ensurePlaybackUri = useCallback(async () => {
        if (playbackUri) return playbackUri;
        if (!mediaAssetId) throw new Error("Voice message has no media asset id.");

        setIsResolving(true);
        try {
            const { downloadUrl } = await apiClient.media.getAssetDownloadUrl(mediaAssetId);
            setPlaybackUri(downloadUrl);
            player.replace({ uri: downloadUrl });
            return downloadUrl;
        } finally {
            setIsResolving(false);
        }
    }, [mediaAssetId, playbackUri, player]);

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
                    isOwn ? "bg-white/20 active:bg-white/30" : "bg-brand-600 active:bg-brand-500"
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
                    <Text className="text-[11px] font-medium text-white/80">
                        {formatVoiceDuration(currentSeconds)}
                    </Text>
                    <Text className="text-[11px] font-medium text-white/80">
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
