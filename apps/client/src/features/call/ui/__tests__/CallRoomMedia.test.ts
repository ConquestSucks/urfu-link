import {
    buildScreenShareAudioKey,
    isActiveScreenShareAudio,
    normalizeSelectedStageItem,
    pickActiveScreenShare,
    resolveDefaultStageItem,
    SCREEN_SHARE_CAPTURE_OPTIONS,
    shouldDisableLocalScreenShareForRemoteOwner,
    type ScreenShareCandidate,
    type StageMediaItem,
} from "../CallRoomMedia";

const candidate = (
    ownerId: string,
    key: string,
    isLocal = false,
): ScreenShareCandidate => ({
    ownerId,
    key,
    isLocal,
});

describe("CallRoomMedia", () => {
    it("keeps the local presenter as active immediately after local screen share starts", () => {
        expect(
            pickActiveScreenShare(
                [
                    candidate("remote-1", "remote-old"),
                    candidate("user-1", "local-new", true),
                ],
                "user-1",
                "user-1",
            ),
        ).toEqual(candidate("user-1", "local-new", true));
    });

    it("keeps a remote winner while the local screen share is being unpublished", () => {
        expect(
            pickActiveScreenShare(
                [
                    candidate("remote-1", "remote-old"),
                    candidate("user-1", "local-pending-stop", true),
                    candidate("remote-2", "remote-new"),
                ],
                "remote-2",
                "user-1",
            ),
        ).toEqual(candidate("remote-2", "remote-new"));
    });

    it("uses last remote presenter when multiple remote screen shares race", () => {
        expect(
            pickActiveScreenShare(
                [
                    candidate("remote-1", "remote-old"),
                    candidate("remote-2", "remote-new"),
                ],
                "remote-1",
                "user-1",
            ),
        ).toEqual(candidate("remote-2", "remote-new"));
    });

    it("tells the local client to stop sharing when a remote owner wins", () => {
        expect(shouldDisableLocalScreenShareForRemoteOwner("user-1", "remote-2", true))
            .toBe(true);
        expect(shouldDisableLocalScreenShareForRemoteOwner("user-1", "user-1", true))
            .toBe(false);
        expect(shouldDisableLocalScreenShareForRemoteOwner("user-1", "remote-2", false))
            .toBe(false);
    });

    it("uses screen share as the default stage item when it is available", () => {
        const items: StageMediaItem[] = [
            {
                id: "participant:user-1",
                kind: "participant",
                ownerId: "user-1",
                hasCamera: true,
                isSpeaking: true,
                isConnected: true,
            },
            {
                id: "screen:user-2:screen-track",
                kind: "screen",
                ownerId: "user-2",
                trackKey: "screen-track",
                hasCamera: false,
                isSpeaking: false,
                isConnected: true,
            },
        ];

        expect(resolveDefaultStageItem(items, new Set(["user-1"]))?.id)
            .toBe("screen:user-2:screen-track");
    });

    it("falls back to speaking, camera, connected, then first participant stage item", () => {
        const items: StageMediaItem[] = [
            {
                id: "participant:user-1",
                kind: "participant",
                ownerId: "user-1",
                hasCamera: false,
                isSpeaking: false,
                isConnected: false,
            },
            {
                id: "participant:user-2",
                kind: "participant",
                ownerId: "user-2",
                hasCamera: true,
                isSpeaking: false,
                isConnected: true,
            },
            {
                id: "participant:user-3",
                kind: "participant",
                ownerId: "user-3",
                hasCamera: false,
                isSpeaking: true,
                isConnected: true,
            },
        ];

        expect(resolveDefaultStageItem(items, new Set(["user-3"]))?.id)
            .toBe("participant:user-3");
        expect(resolveDefaultStageItem(
            items.map((item) => ({ ...item, isSpeaking: false })),
            new Set(),
        )?.id)
            .toBe("participant:user-2");
        expect(resolveDefaultStageItem([items[0]], new Set())?.id)
            .toBe("participant:user-1");
    });

    it("keeps a manually selected stage item only while it still exists", () => {
        const items: StageMediaItem[] = [
            {
                id: "participant:user-1",
                kind: "participant",
                ownerId: "user-1",
                hasCamera: false,
                isSpeaking: false,
                isConnected: true,
            },
            {
                id: "screen:user-2:screen-track",
                kind: "screen",
                ownerId: "user-2",
                trackKey: "screen-track",
                hasCamera: false,
                isSpeaking: false,
                isConnected: true,
            },
        ];

        expect(normalizeSelectedStageItem("participant:user-1", items)?.id)
            .toBe("participant:user-1");
        expect(normalizeSelectedStageItem("participant:missing", items)?.id)
            .toBe("screen:user-2:screen-track");
    });

    it("builds stable screen share audio keys", () => {
        expect(buildScreenShareAudioKey("user-1", "track-1")).toBe("screen-audio:user-1:track-1");
        expect(buildScreenShareAudioKey("user-1", null)).toBe("screen-audio:user-1:unknown");
    });

    it("uses browser screen capture options that request share audio", () => {
        expect(SCREEN_SHARE_CAPTURE_OPTIONS).toEqual(
            expect.objectContaining({
                audio: true,
                systemAudio: "include",
                surfaceSwitching: "include",
                contentHint: "detail",
            }),
        );
    });

    it("only plays screen-share audio for the active remote owner", () => {
        expect(isActiveScreenShareAudio("remote-1", "remote-1", false)).toBe(true);
        expect(isActiveScreenShareAudio("remote-2", "remote-1", false)).toBe(false);
        expect(isActiveScreenShareAudio("user-1", "user-1", true)).toBe(false);
    });
});
