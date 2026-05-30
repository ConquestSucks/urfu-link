import { createAudioPlayer, setAudioModeAsync } from "expo-audio";
import {
    configureCallSounds,
    playCallSound,
    resetCallSoundsForTests,
    setCallSoundTestOverrides,
    startCallRingtone,
    stopCallRingtone,
} from "../call-sounds";

jest.mock("expo-audio", () => ({
    createAudioPlayer: jest.fn(),
    setAudioModeAsync: jest.fn(async () => undefined),
}));

const players: Array<{
    play: jest.Mock;
    pause: jest.Mock;
    seekTo: jest.Mock;
    playing: boolean;
    loop: boolean;
}> = [];

describe("call sounds", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        players.length = 0;
        (createAudioPlayer as jest.Mock).mockImplementation(() => {
            const player = {
                play: jest.fn(),
                pause: jest.fn(),
                seekTo: jest.fn(async () => undefined),
                playing: false,
                loop: false,
            };
            players.push(player);
            return player;
        });
        resetCallSoundsForTests();
        setCallSoundTestOverrides({
            audioModule: {
                createAudioPlayer: createAudioPlayer as never,
                setAudioModeAsync: setAudioModeAsync as never,
            },
            sources: {
                outgoing: "call-outgoing.wav",
                incoming: "call-incoming.wav",
                micOn: "call-mic-on.wav",
                micOff: "call-mic-off.wav",
                leave: "call-leave.wav",
            },
        });
    });

    it("loops and stops outgoing ringtone", async () => {
        await expect(startCallRingtone("outgoing")).resolves.toBe(true);

        expect(createAudioPlayer).toHaveBeenCalledTimes(1);
        expect(players[0].loop).toBe(true);
        expect(players[0].seekTo).toHaveBeenCalledWith(0);
        expect(players[0].play).toHaveBeenCalledTimes(1);

        await stopCallRingtone("outgoing");

        expect(players[0].pause).toHaveBeenCalledTimes(1);
        expect(players[0].loop).toBe(false);
    });

    it("plays one-shot control sounds without looping", async () => {
        await expect(playCallSound("micOff")).resolves.toBe(true);

        expect(createAudioPlayer).toHaveBeenCalledTimes(1);
        expect(players[0].loop).toBe(false);
        expect(players[0].play).toHaveBeenCalledTimes(1);
    });

    it("only exposes the supported call sound kinds", async () => {
        await expect(playCallSound("micOn")).resolves.toBe(true);
        await expect(playCallSound("micOff")).resolves.toBe(true);
        await expect(playCallSound("leave")).resolves.toBe(true);

        // @ts-expect-error camera toggle sounds were intentionally removed.
        const unsupportedCameraOn: Parameters<typeof playCallSound>[0] = "cameraOn";
        // @ts-expect-error camera toggle sounds were intentionally removed.
        const unsupportedCameraOff: Parameters<typeof playCallSound>[0] = "cameraOff";
        expect([unsupportedCameraOn, unsupportedCameraOff]).toEqual(["cameraOn", "cameraOff"]);
    });

    it("respects global sound preferences", async () => {
        configureCallSounds({
            newMessages: true,
            notificationSound: false,
            disciplineChatMessages: true,
            mentions: true,
            mutedConversationIds: [],
        });

        await expect(startCallRingtone("incoming")).resolves.toBe(false);
        await expect(playCallSound("leave")).resolves.toBe(false);

        expect(createAudioPlayer).not.toHaveBeenCalled();
    });
});
