import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { setAudioModeAsync, useAudioPlayer, useAudioPlayerStatus } from "expo-audio";
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

const configurePlaybackMode = () =>
    setAudioModeAsync({
        allowsRecording: false,
        playsInSilentMode: true,
        shouldPlayInBackground: false,
        shouldRouteThroughEarpiece: false,
    }).catch(() => undefined);

const WAVEFORM_BARS = [
    8, 10, 8, 14, 18, 12, 22, 28, 18, 12, 16, 24, 30, 18, 12, 20, 26, 16, 10,
    14, 22, 18, 30, 24, 14, 10, 16, 22, 28, 18, 12, 20, 26, 18, 12, 16, 22,
];
const WAVEFORM_BAR_COUNT = WAVEFORM_BARS.length;
const WAVEFORM_MIN_HEIGHT = 6;
const WAVEFORM_MAX_HEIGHT = 30;
const WAVEFORM_SAMPLE_LIMIT_PER_BAR = 700;

type DecodedAudioBuffer = {
    length: number;
    numberOfChannels: number;
    getChannelData: (channel: number) => Float32Array;
};

type BrowserAudioContext = {
    decodeAudioData: (
        data: ArrayBuffer,
        onSuccess?: (buffer: DecodedAudioBuffer) => void,
        onError?: (error: unknown) => void,
    ) => Promise<DecodedAudioBuffer> | void;
    close?: () => Promise<void>;
};

type BrowserAudioContextConstructor = new () => BrowserAudioContext;

const getBrowserAudioContextConstructor = (): BrowserAudioContextConstructor | null => {
    const audioGlobal = globalThis as typeof globalThis & {
        AudioContext?: BrowserAudioContextConstructor;
        webkitAudioContext?: BrowserAudioContextConstructor;
    };

    return audioGlobal.AudioContext ?? audioGlobal.webkitAudioContext ?? null;
};

const decodeAudioData = (
    context: BrowserAudioContext,
    data: ArrayBuffer,
): Promise<DecodedAudioBuffer> =>
    new Promise((resolve, reject) => {
        const result = context.decodeAudioData(data, resolve, reject);
        if (result && typeof result.then === "function") {
            result.then(resolve, reject);
        }
    });

const levelToWaveformHeight = (level: number) =>
    WAVEFORM_MIN_HEIGHT +
    Math.max(0, Math.min(level, 1)) * (WAVEFORM_MAX_HEIGHT - WAVEFORM_MIN_HEIGHT);

const normalizeWaveformLevels = (levels: number[]) => {
    const maxLevel = Math.max(...levels);
    if (!Number.isFinite(maxLevel) || maxLevel <= 0.0001) {
        return Array.from({ length: WAVEFORM_BAR_COUNT }, () => WAVEFORM_MIN_HEIGHT);
    }

    return levels.map((level) => levelToWaveformHeight(Math.sqrt(level / maxLevel)));
};

const buildWaveformFromAudioBuffer = (audioBuffer: DecodedAudioBuffer) => {
    if (audioBuffer.length <= 0 || audioBuffer.numberOfChannels <= 0) return null;

    const levels = Array.from({ length: WAVEFORM_BAR_COUNT }, (_, barIndex) => {
        const start = Math.floor((barIndex / WAVEFORM_BAR_COUNT) * audioBuffer.length);
        const end = Math.max(
            start + 1,
            Math.floor(((barIndex + 1) / WAVEFORM_BAR_COUNT) * audioBuffer.length),
        );
        const stride = Math.max(
            1,
            Math.floor((end - start) / WAVEFORM_SAMPLE_LIMIT_PER_BAR),
        );
        let sumSquares = 0;
        let sampleCount = 0;

        for (let channel = 0; channel < audioBuffer.numberOfChannels; channel += 1) {
            const channelData = audioBuffer.getChannelData(channel);
            for (let sampleIndex = start; sampleIndex < end; sampleIndex += stride) {
                const sample = channelData[sampleIndex] ?? 0;
                sumSquares += sample * sample;
                sampleCount += 1;
            }
        }

        return sampleCount > 0 ? Math.sqrt(sumSquares / sampleCount) : 0;
    });

    return normalizeWaveformLevels(levels);
};

const extractWaveformBars = async (uri: string) => {
    const AudioContextConstructor = getBrowserAudioContextConstructor();
    if (!AudioContextConstructor || typeof fetch !== "function") return null;

    const context = new AudioContextConstructor();
    try {
        const response = await fetch(uri);
        const audioData = await response.arrayBuffer();
        const decodedAudio = await decodeAudioData(context, audioData);
        return buildWaveformFromAudioBuffer(decodedAudio);
    } finally {
        await context.close?.().catch(() => undefined);
    }
};

const getTimelineLocationX = (event: any, width: number) => {
    const nativeEvent = event.nativeEvent ?? event;
    const directLocation =
        nativeEvent.locationX ??
        nativeEvent.offsetX ??
        nativeEvent.layerX ??
        nativeEvent.nativeEvent?.locationX;

    if (typeof directLocation === "number" && Number.isFinite(directLocation)) {
        return Math.max(0, Math.min(directLocation, width));
    }

    const clientX =
        nativeEvent.clientX ??
        nativeEvent.changedTouches?.[0]?.clientX ??
        nativeEvent.touches?.[0]?.clientX;
    const pageX =
        nativeEvent.pageX ??
        nativeEvent.changedTouches?.[0]?.pageX ??
        nativeEvent.touches?.[0]?.pageX;
    const target = event.currentTarget ?? nativeEvent.currentTarget ?? nativeEvent.target;
    const rect =
        target && typeof target.getBoundingClientRect === "function"
            ? target.getBoundingClientRect()
            : null;

    if (typeof clientX === "number" && rect) {
        return Math.max(0, Math.min(clientX - rect.left, width));
    }

    if (typeof pageX === "number" && rect) {
        const viewport = globalThis as typeof globalThis & {
            scrollX?: number;
            pageXOffset?: number;
        };
        const scrollX = viewport.scrollX ?? viewport.pageXOffset ?? 0;
        return Math.max(0, Math.min(pageX - rect.left - scrollX, width));
    }

    return null;
};

const formatFileSize = (size?: number | null): string | null => {
    if (typeof size !== "number" || size <= 0) return null;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    if (size < 1024 * 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
    return `${(size / 1024 / 1024 / 1024).toFixed(1)} GB`;
};

type ProgressFrameHandle = ReturnType<typeof setInterval>;

const getTimestamp = () => globalThis.performance?.now?.() ?? Date.now();

const requestProgressFrame = (callback: () => void): ProgressFrameHandle =>
    setInterval(callback, 80);

const cancelProgressFrame = (handle: ProgressFrameHandle | null) => {
    if (!handle) return;
    clearInterval(handle);
};

export const VoiceMessagePlayer = ({
    sourceUri,
    mediaAssetId,
    durationSeconds,
    fileSizeBytes,
    isOwn = false,
    compact = false,
}: VoiceMessagePlayerProps) => {
    const [playbackUri, setPlaybackUri] = useState<string | null>(sourceUri ?? null);
    const [isResolving, setIsResolving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [timelineWidth, setTimelineWidth] = useState(0);
    const [waveformBars, setWaveformBars] = useState(WAVEFORM_BARS);
    const loadedUriRef = useRef<string | null>(null);
    const waveformRequestRef = useRef(0);
    const progressFrameRef = useRef<ProgressFrameHandle | null>(null);
    const playbackClockRef = useRef({ startedAt: 0, startedFrom: 0 });
    const player = useAudioPlayer(null);
    const status = useAudioPlayerStatus(player);
    const [displaySeconds, setDisplaySeconds] = useState(() =>
        Math.max(0, status.currentTime ?? 0),
    );

    const replacePlaybackSource = useCallback(
        (uri: string) => {
            if (uri === loadedUriRef.current) return;
            loadedUriRef.current = uri;
            player.replace({ uri });
        },
        [player],
    );

    useEffect(() => {
        if (!sourceUri) return;
        setPlaybackUri(sourceUri);
        replacePlaybackSource(sourceUri);
    }, [replacePlaybackSource, sourceUri]);

    useEffect(() => {
        const waveformUri = playbackUri ?? sourceUri;
        const requestId = waveformRequestRef.current + 1;
        waveformRequestRef.current = requestId;

        if (!waveformUri) {
            setWaveformBars(WAVEFORM_BARS);
            return undefined;
        }

        let isActive = true;
        setWaveformBars(WAVEFORM_BARS);
        extractWaveformBars(waveformUri)
            .then((bars) => {
                if (!isActive || waveformRequestRef.current !== requestId || !bars) return;
                setWaveformBars(bars);
            })
            .catch(() => undefined);

        return () => {
            isActive = false;
        };
    }, [playbackUri, sourceUri]);

    const totalSeconds = useMemo(() => {
        const metadataDuration = durationSeconds && durationSeconds > 0 ? durationSeconds : 0;
        const playerDuration = status.duration && status.duration > 0 ? status.duration : 0;

        if (metadataDuration > 0 && playerDuration > 0) {
            return Math.min(metadataDuration, playerDuration);
        }
        return metadataDuration || playerDuration;
    }, [durationSeconds, status.duration]);
    const statusSeconds = Math.max(
        0,
        Math.min(status.currentTime ?? 0, totalSeconds || Number.MAX_SAFE_INTEGER),
    );
    const currentSeconds = Math.max(
        0,
        Math.min(displaySeconds, totalSeconds || Number.MAX_SAFE_INTEGER),
    );
    const progress = totalSeconds > 0 ? Math.min(currentSeconds / totalSeconds, 1) : 0;
    const hasFinished = totalSeconds > 0 && currentSeconds >= totalSeconds - 0.05 && !status.playing;
    const isReadyToPlay = Boolean(playbackUri);
    const sizeLabel = useMemo(() => formatFileSize(fileSizeBytes), [fileSizeBytes]);
    const durationLabel =
        totalSeconds > 0 && !status.playing && currentSeconds <= 0.05
            ? formatVoiceDuration(totalSeconds)
            : formatVoiceDuration(currentSeconds);
    const metadataLabel = [durationLabel, sizeLabel].filter(Boolean).join(", ");
    const activeWaveColor = isOwn ? "#FFFFFF" : "#60A5FA";
    const inactiveWaveColor = isOwn ? "rgba(255,255,255,0.32)" : "rgba(255,255,255,0.22)";

    useEffect(() => {
        cancelProgressFrame(progressFrameRef.current);
        progressFrameRef.current = null;
        setDisplaySeconds(statusSeconds);

        if (!status.playing) return undefined;

        playbackClockRef.current = {
            startedAt: getTimestamp(),
            startedFrom: statusSeconds,
        };
        const tick = () => {
            const { startedAt, startedFrom } = playbackClockRef.current;
            const elapsedSeconds = (getTimestamp() - startedAt) / 1000;
            const nextSeconds = Math.max(
                0,
                Math.min(startedFrom + elapsedSeconds, totalSeconds || Number.MAX_SAFE_INTEGER),
            );
            setDisplaySeconds(nextSeconds);

            if (totalSeconds > 0 && nextSeconds >= totalSeconds - 0.02) {
                cancelProgressFrame(progressFrameRef.current);
                progressFrameRef.current = null;
            }
        };

        progressFrameRef.current = requestProgressFrame(tick);
        return () => {
            cancelProgressFrame(progressFrameRef.current);
            progressFrameRef.current = null;
        };
    }, [status.playing, statusSeconds, totalSeconds]);

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

            if (!isReadyToPlay) {
                await ensurePlaybackUri();
                return;
            }

            await configurePlaybackMode();
            await ensurePlaybackUri();
            if (hasFinished) {
                await player.seekTo(0);
                setDisplaySeconds(0);
            }
            player.play();
        } catch (playError) {
            console.error("Failed to play voice message", playError);
            setError("Не удалось воспроизвести");
        }
    }, [ensurePlaybackUri, hasFinished, isReadyToPlay, player, status.playing]);

    const seekToProgress = useCallback(
        async (nextProgress: number) => {
            if (totalSeconds <= 0 || isResolving) return;

            setError(null);
            try {
                await ensurePlaybackUri();
                const nextSeconds = Math.max(0, Math.min(nextProgress, 1)) * totalSeconds;
                await player.seekTo(nextSeconds);
                if (status.playing) {
                    playbackClockRef.current = {
                        startedAt: getTimestamp(),
                        startedFrom: nextSeconds,
                    };
                }
                setDisplaySeconds(nextSeconds);
            } catch (seekError) {
                console.error("Failed to seek voice message", seekError);
                setError("Не удалось перемотать");
            }
        },
        [ensurePlaybackUri, isResolving, player, status.playing, totalSeconds],
    );

    const handleTimelinePress = useCallback(
        (event: any) => {
            if (timelineWidth <= 0) return;
            const locationX = getTimelineLocationX(event, timelineWidth);
            if (locationX == null) return;
            void seekToProgress(locationX / timelineWidth);
        },
        [seekToProgress, timelineWidth],
    );

    const renderWaveformBars = (color: string) =>
        waveformBars.map((height, index) => (
            <View
                key={`${color}-${index}`}
                testID="voice-message-waveform-bar"
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
                ) : status.playing ? (
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
                                { width: `${Math.max(progress * 100, status.playing ? 2 : 0)}%` },
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
