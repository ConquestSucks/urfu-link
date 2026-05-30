import React from "react";
import { Pressable, Text, View } from "react-native";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { StopCircleIcon, XIcon } from "@/shared/ui/phosphor";
import { formatVoiceDuration } from "../lib/format";

type VoiceRecorderBarProps = {
    durationSeconds: number;
    maxDurationSeconds: number;
    isStopping?: boolean;
    onStop: () => void | Promise<void>;
    onCancel: () => void | Promise<void>;
};

export const VoiceRecorderBar = ({
    durationSeconds,
    maxDurationSeconds,
    isStopping = false,
    onStop,
    onCancel,
}: VoiceRecorderBarProps) => (
    <View
        testID="voice-recorder-bar"
        className="flex-row items-center gap-3 rounded-2xl border border-danger-400/25 bg-danger-500/10 px-3 py-2.5"
    >
        <View className="w-2.5 h-2.5 rounded-full bg-danger-400" />
        <View className="flex-1 min-w-0">
            <Text className="text-white text-[14px] font-semibold">Запись голоса</Text>
            <Text className="text-text-subtle text-[12px] font-medium">
                {formatVoiceDuration(durationSeconds)} / {formatVoiceDuration(maxDurationSeconds)}
            </Text>
        </View>
        <Pressable
            testID="voice-recorder-cancel"
            onPress={onCancel}
            disabled={isStopping}
            className={`w-10 h-10 rounded-full items-center justify-center bg-white/10 ${
                isStopping ? "opacity-50" : "active:bg-white/20"
            }`}
        >
            <XIcon size={18} className="text-white" />
        </Pressable>
        <Pressable
            testID="voice-recorder-stop"
            onPress={onStop}
            disabled={isStopping}
            className={`w-10 h-10 rounded-full items-center justify-center bg-danger-500 ${
                isStopping ? "opacity-70" : "active:bg-danger-400"
            }`}
        >
            {isStopping ? (
                <ActivityIndicator size="small" className="text-white" />
            ) : (
                <StopCircleIcon size={22} className="text-white" weight="fill" />
            )}
        </Pressable>
    </View>
);
