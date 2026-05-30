export type ScreenShareCandidate = {
    ownerId: string;
    key: string;
    isLocal: boolean;
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
