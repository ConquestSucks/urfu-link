import React, { useEffect, useMemo, useRef } from "react";
import { Animated, Pressable, StyleSheet, Text, View } from "react-native";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { StopCircleIcon, XIcon } from "@/shared/ui/phosphor";
import { glow } from "@/shared/ui/glow";
import { formatVoiceDuration } from "../lib/format";

type VoiceRecorderBarProps = {
    durationSeconds: number;
    maxDurationSeconds: number;
    isStopping?: boolean;
    onStop: () => void | Promise<void>;
    onCancel: () => void | Promise<void>;
};

const RECORDING_BARS = [8, 12, 18, 14, 24, 30, 18, 12, 22, 28, 16, 10, 18, 26, 20, 12];

export const VoiceRecorderBar = ({
    durationSeconds,
    maxDurationSeconds,
    isStopping = false,
    onStop,
    onCancel,
}: VoiceRecorderBarProps) => {
    const pulse = useRef(new Animated.Value(0)).current;
    const progress = Math.min(1, Math.max(0, durationSeconds / Math.max(1, maxDurationSeconds)));
    const pulseScale = pulse.interpolate({
        inputRange: [0, 1],
        outputRange: [1, 1.75],
    });
    const pulseOpacity = pulse.interpolate({
        inputRange: [0, 1],
        outputRange: [0.45, 0],
    });
    const waveBars = useMemo(
        () =>
            RECORDING_BARS.map((height, index) => {
                const isActive = index / Math.max(1, RECORDING_BARS.length - 1) <= progress;
                return (
                    <View
                        key={index}
                        style={[
                            styles.waveBar,
                            {
                                height,
                                backgroundColor: isActive
                                    ? "#FF637E"
                                    : "rgba(255,255,255,0.18)",
                            },
                        ]}
                    />
                );
            }),
        [progress],
    );

    useEffect(() => {
        const loop = Animated.loop(
            Animated.sequence([
                Animated.timing(pulse, {
                    toValue: 1,
                    duration: 900,
                    useNativeDriver: true,
                }),
                Animated.timing(pulse, {
                    toValue: 0,
                    duration: 0,
                    useNativeDriver: true,
                }),
            ]),
        );
        loop.start();
        return () => loop.stop();
    }, [pulse]);

    return (
        <View
            testID="voice-recorder-bar"
            className="rounded-2xl border border-white/10 bg-white/[0.06] px-3 py-2.5"
            style={glow.surface}
        >
            <View className="flex-row items-center gap-3">
                <View className="w-10 h-10 rounded-full items-center justify-center bg-danger-500/10 overflow-hidden">
                    <Animated.View
                        style={[
                            styles.pulse,
                            {
                                opacity: pulseOpacity,
                                transform: [{ scale: pulseScale }],
                            },
                        ]}
                    />
                    <View className="w-3 h-3 rounded-full bg-danger-300" style={glow.danger} />
                </View>

                <View className="flex-1 min-w-0 gap-1.5">
                    <View className="flex-row items-center justify-between gap-2">
                        <Text className="text-white text-[13px] font-semibold" numberOfLines={1}>
                            Запись
                        </Text>
                        <Text
                            className="text-text-placeholder text-[12px] font-medium"
                            style={styles.tabularText}
                        >
                            {formatVoiceDuration(durationSeconds)} / {formatVoiceDuration(maxDurationSeconds)}
                        </Text>
                    </View>
                    <View style={styles.waveRow}>{waveBars}</View>
                    <View style={styles.progressTrack}>
                        <View style={[styles.progressFill, { width: `${progress * 100}%` }]} />
                    </View>
                </View>

                <Pressable
                    testID="voice-recorder-cancel"
                    onPress={onCancel}
                    disabled={isStopping}
                    className={`w-10 h-10 rounded-full items-center justify-center bg-white/10 ${
                        isStopping ? "opacity-50" : "active:bg-white/20"
                    }`}
                    hitSlop={4}
                >
                    <XIcon size={18} className="text-white" />
                </Pressable>
                <Pressable
                    testID="voice-recorder-stop"
                    onPress={onStop}
                    disabled={isStopping}
                    className={`w-11 h-11 rounded-full items-center justify-center bg-danger-500 ${
                        isStopping ? "opacity-70" : "active:bg-danger-400"
                    }`}
                    style={glow.danger}
                >
                    {isStopping ? (
                        <ActivityIndicator size="small" className="text-white" />
                    ) : (
                        <StopCircleIcon size={22} className="text-white" weight="fill" />
                    )}
                </Pressable>
            </View>
        </View>
    );
};

const styles = StyleSheet.create({
    pulse: {
        position: "absolute",
        width: 18,
        height: 18,
        borderRadius: 999,
        backgroundColor: "#FF637E",
    },
    waveRow: {
        height: 30,
        flexDirection: "row",
        alignItems: "center",
        gap: 2,
        overflow: "hidden",
    },
    waveBar: {
        width: 3,
        borderRadius: 999,
    },
    progressTrack: {
        height: 3,
        borderRadius: 999,
        backgroundColor: "rgba(255,255,255,0.14)",
        overflow: "hidden",
    },
    progressFill: {
        height: 3,
        borderRadius: 999,
        backgroundColor: "#FF637E",
    },
    tabularText: {
        fontVariant: ["tabular-nums"],
    },
});
