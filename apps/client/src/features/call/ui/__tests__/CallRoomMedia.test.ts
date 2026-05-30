import {
    pickActiveScreenShare,
    shouldDisableLocalScreenShareForRemoteOwner,
    type ScreenShareCandidate,
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
});
