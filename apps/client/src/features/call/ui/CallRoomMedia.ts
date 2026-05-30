import type { ScreenShareCaptureOptions } from "livekit-client";
import type { ViewStyle } from "react-native";

export type ScreenShareCandidate = {
    ownerId: string;
    key: string;
    isLocal: boolean;
};

export type StageMediaItem = {
    id: string;
    kind: "participant" | "screen";
    ownerId: string;
    trackKey?: string;
    hasCamera: boolean;
    isSpeaking: boolean;
    isConnected: boolean;
};

export const SCREEN_SHARE_CAPTURE_OPTIONS: ScreenShareCaptureOptions = {
    audio: true,
    systemAudio: "include",
    surfaceSwitching: "include",
    contentHint: "detail",
};

export const pickActiveScreenShare = (
    candidates: ScreenShareCandidate[],
    previousOwnerId: string | null,
    localUserId: string | null,
): ScreenShareCandidate | null => {
    if (candidates.length === 0) return null;

    const previousCandidate = previousOwnerId
        ? candidates.find((candidate) => candidate.ownerId === previousOwnerId)
        : undefined;
    if (previousCandidate && candidates[candidates.length - 1]?.ownerId === previousOwnerId) {
        return previousCandidate;
    }

    const localCandidate = localUserId
        ? candidates.find((candidate) => candidate.isLocal && candidate.ownerId === localUserId)
        : undefined;
    if (localCandidate) return localCandidate;

    return candidates[candidates.length - 1] ?? null;
};

export const shouldDisableLocalScreenShareForRemoteOwner = (
    localUserId: string | null,
    activeOwnerId: string | null,
    localScreenShareEnabled: boolean,
) =>
    Boolean(
        localUserId &&
        activeOwnerId &&
        activeOwnerId !== localUserId &&
        localScreenShareEnabled,
    );

export const resolveDefaultStageItem = (
    items: StageMediaItem[],
    activeSpeakerIds: ReadonlySet<string>,
): StageMediaItem | null => {
    if (items.length === 0) return null;

    return (
        items.find((item) => item.kind === "screen") ??
        items.find((item) => activeSpeakerIds.has(item.ownerId) || item.isSpeaking) ??
        items.find((item) => item.kind === "participant" && item.hasCamera) ??
        items.find((item) => item.isConnected) ??
        items[0] ??
        null
    );
};

export const normalizeSelectedStageItem = (
    selectedId: string | null,
    items: StageMediaItem[],
    activeSpeakerIds: ReadonlySet<string> = new Set(),
): StageMediaItem | null => {
    if (selectedId) {
        const selected = items.find((item) => item.id === selectedId);
        if (selected) return selected;
    }

    return resolveDefaultStageItem(items, activeSpeakerIds);
};

export const buildScreenShareAudioKey = (
    ownerId: string,
    trackSid: string | null | undefined,
) => `screen-audio:${ownerId}:${trackSid ?? "unknown"}`;

export const isActiveScreenShareAudio = (
    ownerId: string,
    activeOwnerId: string | null,
    isLocal: boolean,
) => Boolean(activeOwnerId && ownerId === activeOwnerId && !isLocal);

export const getCallStageLayoutStyles = (
    isMobile: boolean,
): { container: ViewStyle; main: ViewStyle; rail: ViewStyle } => ({
    container: {
        alignSelf: "stretch",
        flexGrow: 1,
        flexShrink: 1,
        minHeight: 0,
        minWidth: 0,
        width: "100%",
    },
    main: {
        alignSelf: "stretch",
        flexBasis: isMobile ? "auto" : 0,
        flexGrow: 1,
        flexShrink: 1,
        minHeight: 0,
        minWidth: 0,
        width: "100%",
    },
    rail: isMobile
        ? {
              flexShrink: 0,
              maxHeight: 132,
              width: "100%",
          }
        : {
              alignSelf: "stretch",
              flexShrink: 0,
              minHeight: 0,
              width: 176,
          },
});

export const getParticipantRailCardStyle = (
    isMobile: boolean,
    hasScreenShare: boolean,
): ViewStyle & { width: number; height: number } => {
    if (isMobile) {
        return hasScreenShare
            ? { width: 128, height: 108 }
            : { width: 112, height: 96 };
    }

    return hasScreenShare
        ? { width: 176, height: 128 }
        : { width: 160, height: 112 };
};
