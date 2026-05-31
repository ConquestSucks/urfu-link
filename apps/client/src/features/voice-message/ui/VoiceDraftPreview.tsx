import React from "react";
import { Pressable, View } from "react-native";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { PaperPlaneRightIcon, TrashIcon } from "@/shared/ui/phosphor";
import { glow } from "@/shared/ui/glow";
import type { VoiceRecordingDraft } from "../model/types";
import { VoiceMessagePlayer } from "./VoiceMessagePlayer";

type VoiceDraftPreviewProps = {
    draft: VoiceRecordingDraft;
    isSubmitting?: boolean;
    onDelete: () => void;
    onSubmit: () => void | Promise<void>;
};

export const VoiceDraftPreview = ({
    draft,
    isSubmitting = false,
    onDelete,
    onSubmit,
}: VoiceDraftPreviewProps) => (
    <View
        testID="voice-draft-preview"
        className="flex-row items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.06] px-3 py-2.5"
        style={glow.surface}
    >
        <View className="flex-1 min-w-0">
            <VoiceMessagePlayer
                sourceUri={draft.uri}
                durationSeconds={draft.durationSeconds}
                compact
            />
        </View>
        <Pressable
            testID="voice-draft-delete"
            onPress={onDelete}
            disabled={isSubmitting}
            className={`w-10 h-10 rounded-full items-center justify-center bg-white/10 ${
                isSubmitting ? "opacity-40" : "active:bg-white/20"
            }`}
        >
            <TrashIcon size={18} className="text-text-subtle" />
        </Pressable>
        <Pressable
            testID="voice-draft-submit"
            onPress={onSubmit}
            disabled={isSubmitting}
            className={`w-11 h-11 rounded-full items-center justify-center bg-brand-650 ${
                isSubmitting ? "opacity-70" : "active:opacity-80"
            }`}
            style={glow.brandSoft}
        >
            {isSubmitting ? (
                <ActivityIndicator size="small" className="text-white" />
            ) : (
                <PaperPlaneRightIcon size={21} className="text-white" weight="fill" />
            )}
        </Pressable>
    </View>
);
