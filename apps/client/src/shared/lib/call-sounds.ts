import type { UserNotifications } from "@urfu-link/api-client";

export type CallSoundKind =
    | "outgoing"
    | "incoming"
    | "micOn"
    | "micOff"
    | "leave";

type RingtoneKind = Extract<CallSoundKind, "outgoing" | "incoming">;
type ExpoAudioModule = typeof import("expo-audio");
type AudioPlayer = import("expo-audio").AudioPlayer;
type AudioSource = import("expo-audio").AudioSource;
type AudioRuntime = Pick<ExpoAudioModule, "createAudioPlayer" | "setAudioModeAsync">;

const DEFAULT_PREFERENCES: UserNotifications = {
    newMessages: true,
    notificationSound: true,
    disciplineChatMessages: true,
    mentions: true,
    mutedConversationIds: [],
};

let preferences: UserNotifications = DEFAULT_PREFERENCES;
let audioModulePromise: Promise<ExpoAudioModule> | null = null;
let audioModuleOverride: AudioRuntime | null = null;
let soundSourcesOverride: Partial<Record<CallSoundKind, AudioSource>> | null = null;
let audioModeConfigured = false;
let players: Partial<Record<CallSoundKind, AudioPlayer>> = {};

const getAudioModule = () => {
    if (audioModuleOverride) return Promise.resolve(audioModuleOverride);
    audioModulePromise ??= import("expo-audio");
    return audioModulePromise;
};

const SOUND_SOURCES: Record<CallSoundKind, AudioSource> = {
    outgoing: require("../../../assets/sounds/call-outgoing.wav"),
    incoming: require("../../../assets/sounds/call-incoming.wav"),
    micOn: require("../../../assets/sounds/call-mic-on.wav"),
    micOff: require("../../../assets/sounds/call-mic-off.wav"),
    leave: require("../../../assets/sounds/call-leave.wav"),
};

const getSoundSource = (kind: CallSoundKind) =>
    soundSourcesOverride?.[kind] ?? SOUND_SOURCES[kind];

const configureAudioMode = async (audio: AudioRuntime) => {
    if (audioModeConfigured) return;

    await audio.setAudioModeAsync({
        allowsRecording: false,
        interruptionMode: "mixWithOthers",
        playsInSilentMode: true,
        shouldPlayInBackground: false,
        shouldRouteThroughEarpiece: false,
    });
    audioModeConfigured = true;
};

const shouldPlaySound = () => preferences.notificationSound;

const getPlayer = async (kind: CallSoundKind) => {
    const audio = await getAudioModule();
    await configureAudioMode(audio);

    players[kind] ??= audio.createAudioPlayer(getSoundSource(kind), {
        updateInterval: 1000,
        keepAudioSessionActive: false,
    });

    return players[kind]!;
};

export const configureCallSounds = (nextPreferences: UserNotifications | null | undefined) => {
    preferences = {
        ...(nextPreferences ?? DEFAULT_PREFERENCES),
        mutedConversationIds: nextPreferences?.mutedConversationIds ?? [],
    };
};

export const playCallSound = async (kind: CallSoundKind): Promise<boolean> => {
    if (!shouldPlaySound()) return false;

    try {
        const player = await getPlayer(kind);
        player.loop = false;
        if (player.playing) player.pause();
        await player.seekTo(0).catch(() => undefined);
        player.play();
        return true;
    } catch {
        return false;
    }
};

export const startCallRingtone = async (kind: RingtoneKind): Promise<boolean> => {
    if (!shouldPlaySound()) return false;

    try {
        const player = await getPlayer(kind);
        player.loop = true;
        if (player.playing) player.pause();
        await player.seekTo(0).catch(() => undefined);
        player.play();
        return true;
    } catch {
        return false;
    }
};

export const stopCallRingtone = async (kind?: RingtoneKind) => {
    const kinds: RingtoneKind[] = kind ? [kind] : ["incoming", "outgoing"];

    await Promise.all(
        kinds.map(async (item) => {
            const player = players[item];
            if (!player) return;
            player.pause();
            player.loop = false;
            await player.seekTo(0).catch(() => undefined);
        }),
    );
};

export const resetCallSoundsForTests = () => {
    preferences = DEFAULT_PREFERENCES;
    audioModulePromise = null;
    audioModuleOverride = null;
    soundSourcesOverride = null;
    audioModeConfigured = false;
    players = {};
};

export const setCallSoundTestOverrides = (overrides: {
    audioModule?: AudioRuntime;
    sources?: Partial<Record<CallSoundKind, AudioSource>>;
}) => {
    audioModuleOverride = overrides.audioModule ?? null;
    soundSourcesOverride = overrides.sources ?? null;
};
