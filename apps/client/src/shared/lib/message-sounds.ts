import type { UserNotifications } from "@urfu-link/api-client";

type SoundKind = "send" | "receive";

type MessageSoundContext = {
    conversationId?: string | null;
    isDiscipline?: boolean;
    now?: number;
};

type ExpoAudioModule = typeof import("expo-audio");
type AudioPlayer = import("expo-audio").AudioPlayer;
type AudioSource = import("expo-audio").AudioSource;
type AudioRuntime = Pick<ExpoAudioModule, "createAudioPlayer" | "setAudioModeAsync">;

const RECEIVE_THROTTLE_MS = 400;
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
let soundSourcesOverride: Partial<Record<SoundKind, AudioSource>> | null = null;
let audioModeConfigured = false;
let players: Partial<Record<SoundKind, AudioPlayer>> = {};
let lastReceivePlayedAt = 0;

const getAudioModule = () => {
    if (audioModuleOverride) return Promise.resolve(audioModuleOverride);
    audioModulePromise ??= import("expo-audio");
    return audioModulePromise;
};

const getSoundSource = (kind: SoundKind) =>
    soundSourcesOverride?.[kind] ??
    kind === "send"
        ? require("../../../assets/sounds/message-send.wav")
        : require("../../../assets/sounds/message-receive.wav");

const isDisciplineConversation = (context?: MessageSoundContext) =>
    context?.isDiscipline ?? context?.conversationId?.startsWith("discipline:") ?? false;

const isMutedConversation = (conversationId?: string | null) =>
    !!conversationId && preferences.mutedConversationIds.includes(conversationId);

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

const shouldPlaySound = (kind: SoundKind, context?: MessageSoundContext) => {
    if (!preferences.notificationSound) return false;

    if (kind === "send") return true;
    if (isMutedConversation(context?.conversationId)) return false;

    const isDiscipline = isDisciplineConversation(context);
    if (isDiscipline && !preferences.disciplineChatMessages) return false;

    const now = context?.now ?? Date.now();
    if (now - lastReceivePlayedAt < RECEIVE_THROTTLE_MS) return false;

    lastReceivePlayedAt = now;
    return true;
};

const getPlayer = async (kind: SoundKind) => {
    const audio = await getAudioModule();
    await configureAudioMode(audio);

    players[kind] ??= audio.createAudioPlayer(getSoundSource(kind), {
        updateInterval: 1000,
        keepAudioSessionActive: false,
    });

    return players[kind]!;
};

export const configureMessageSounds = (nextPreferences: UserNotifications | null | undefined) => {
    preferences = {
        ...(nextPreferences ?? DEFAULT_PREFERENCES),
        mutedConversationIds: nextPreferences?.mutedConversationIds ?? [],
    };
};

export const playMessageSound = async (
    kind: SoundKind,
    context?: MessageSoundContext,
): Promise<boolean> => {
    if (!shouldPlaySound(kind, context)) return false;

    try {
        const player = await getPlayer(kind);
        if (player.playing) player.pause();
        await player.seekTo(0).catch(() => undefined);
        player.play();
        return true;
    } catch {
        return false;
    }
};

export const resetMessageSoundsForTests = () => {
    preferences = DEFAULT_PREFERENCES;
    audioModulePromise = null;
    audioModuleOverride = null;
    soundSourcesOverride = null;
    audioModeConfigured = false;
    players = {};
    lastReceivePlayedAt = 0;
};

export const setMessageSoundTestOverrides = (overrides: {
    audioModule?: AudioRuntime;
    sources?: Partial<Record<SoundKind, AudioSource>>;
}) => {
    audioModuleOverride = overrides.audioModule ?? null;
    soundSourcesOverride = overrides.sources ?? null;
};
