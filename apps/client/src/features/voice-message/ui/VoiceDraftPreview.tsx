import React from "react";
import { Pressable, View } from "react-native";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import { PaperPlaneRightIcon, TrashIcon } from "@/shared/ui/phosphor";
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
        className="flex-row items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-3 py-2.5"
    >
        <VoiceMessagePlayer
            sourceUri={draft.uri}
            durationSeconds={draft.durationSeconds}
            compact
        />
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
            className={`w-11 h-11 rounded-full items-center justify-center bg-brand-600 ${
                isSubmitting ? "opacity-70" : "active:opacity-80"
            }`}
        >
            {isSubmitting ? (
                <ActivityIndicator size="small" className="text-white" />
            ) : (
                <PaperPlaneRightIcon size={21} className="text-white" weight="fill" />
            )}
        </Pressable>
    </View>
);
