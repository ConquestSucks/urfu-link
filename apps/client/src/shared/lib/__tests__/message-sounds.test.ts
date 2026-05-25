import { createAudioPlayer, setAudioModeAsync } from "expo-audio";
import {
    configureMessageSounds,
    playMessageSound,
    resetMessageSoundsForTests,
    setMessageSoundTestOverrides,
} from "../message-sounds";

jest.mock("expo-audio", () => ({
    createAudioPlayer: jest.fn(),
    setAudioModeAsync: jest.fn(async () => undefined),
}));

const createPlayer = () => ({
    playing: false,
    pause: jest.fn(),
    play: jest.fn(),
    seekTo: jest.fn(async () => undefined),
});

describe("message sounds", () => {
    const players: Array<ReturnType<typeof createPlayer>> = [];

    beforeEach(() => {
        jest.clearAllMocks();
        players.length = 0;
        resetMessageSoundsForTests();
        setMessageSoundTestOverrides({
            audioModule: {
                createAudioPlayer: createAudioPlayer as never,
                setAudioModeAsync: setAudioModeAsync as never,
            },
            sources: {
                send: "message-send.wav",
                receive: "message-receive.wav",
            },
        });

        (createAudioPlayer as jest.Mock).mockImplementation(() => {
            const player = createPlayer();
            players.push(player);
            return player;
        });
    });

    it("plays the send sound from the start", async () => {
        await expect(playMessageSound("send")).resolves.toBe(true);

        expect(setAudioModeAsync).toHaveBeenCalledWith(
            expect.objectContaining({
                interruptionMode: "mixWithOthers",
                playsInSilentMode: true,
            }),
        );
        expect(createAudioPlayer).toHaveBeenCalledTimes(1);
        expect(players[0].seekTo).toHaveBeenCalledWith(0);
        expect(players[0].play).toHaveBeenCalledTimes(1);
    });

    it("does not play when notification sounds are disabled", async () => {
        configureMessageSounds({
            newMessages: true,
            notificationSound: false,
            disciplineChatMessages: true,
            mentions: true,
            mutedConversationIds: [],
        });

        await expect(playMessageSound("send")).resolves.toBe(false);
        await expect(playMessageSound("receive", { conversationId: "direct-1" })).resolves.toBe(
            false,
        );

        expect(createAudioPlayer).not.toHaveBeenCalled();
    });

    it("keeps direct receive sounds independent from the hidden direct-message toggle", async () => {
        configureMessageSounds({
            newMessages: false,
            notificationSound: true,
            disciplineChatMessages: true,
            mentions: true,
            mutedConversationIds: [],
        });

        await expect(
            playMessageSound("receive", { conversationId: "direct-1", now: 1000 }),
        ).resolves.toBe(true);
        await expect(
            playMessageSound("receive", { conversationId: "discipline:math", now: 1400 }),
        ).resolves.toBe(true);

        expect(createAudioPlayer).toHaveBeenCalledTimes(1);
    });

    it("respects discipline and muted conversation preferences", async () => {
        configureMessageSounds({
            newMessages: true,
            notificationSound: true,
            disciplineChatMessages: false,
            mentions: true,
            mutedConversationIds: ["direct-muted"],
        });

        await expect(
            playMessageSound("receive", { conversationId: "discipline:math", now: 1000 }),
        ).resolves.toBe(false);
        await expect(
            playMessageSound("receive", { conversationId: "direct-muted", now: 1000 }),
        ).resolves.toBe(false);
        await expect(
            playMessageSound("receive", { conversationId: "direct-1", now: 1000 }),
        ).resolves.toBe(true);

        expect(createAudioPlayer).toHaveBeenCalledTimes(1);
    });

    it("throttles repeated receive sounds", async () => {
        await expect(
            playMessageSound("receive", { conversationId: "direct-1", now: 1000 }),
        ).resolves.toBe(true);
        await expect(
            playMessageSound("receive", { conversationId: "direct-1", now: 1200 }),
        ).resolves.toBe(false);
        await expect(
            playMessageSound("receive", { conversationId: "direct-1", now: 1400 }),
        ).resolves.toBe(true);

        expect(players[0].play).toHaveBeenCalledTimes(2);
    });

    it("swallows playback errors", async () => {
        (createAudioPlayer as jest.Mock).mockImplementation(() => {
            throw new Error("audio unavailable");
        });

        await expect(playMessageSound("send")).resolves.toBe(false);
    });
});
