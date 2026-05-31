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
    hasScreenShare = false,
): { container: ViewStyle; main: ViewStyle; rail: ViewStyle } => {
    const container: ViewStyle = {
        alignSelf: "stretch",
        flex: 1,
        flexGrow: 1,
        flexShrink: 1,
        height: "100%",
        minHeight: 0,
        minWidth: 0,
        width: "100%",
    };
    const main: ViewStyle = {
        alignSelf: "stretch",
        flex: hasScreenShare && !isMobile ? 2 : 1,
        flexBasis: isMobile ? "auto" : 0,
        flexGrow: hasScreenShare && !isMobile ? 2 : 1,
        flexShrink: 1,
        height: "100%",
        minHeight: 0,
        minWidth: 0,
        ...(hasScreenShare && !isMobile ? {} : { width: "100%" }),
    };

    if (isMobile) {
        return {
            container,
            main,
            rail: {
                flexShrink: 0,
                maxHeight: 156,
                width: "100%",
            },
        };
    }

    return {
        container,
        main,
        rail: hasScreenShare
            ? {
                  alignSelf: "stretch",
                  flex: 1,
                  flexBasis: 0,
                  flexGrow: 1,
                  flexShrink: 1,
                  height: "100%",
                  minHeight: 0,
                  minWidth: 280,
              }
            : {
                  alignSelf: "stretch",
                  flexShrink: 0,
                  minHeight: 0,
                  width: 220,
              },
    };
};

export const getParticipantRailCardStyle = (
    isMobile: boolean,
    hasScreenShare: boolean,
): ViewStyle & { width: number; height: number } => {
    if (isMobile) {
        return hasScreenShare
            ? { width: 148, height: 124 }
            : { width: 132, height: 112 };
    }

    return hasScreenShare
        ? { width: 220, height: 148 }
        : { width: 192, height: 132 };
};
