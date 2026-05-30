import { act, renderHook } from "@testing-library/react-native";
import { AudioModule, setAudioModeAsync } from "expo-audio";
import { useVoiceRecorder } from "../useVoiceRecorder";

type MockRecorderState = {
    isRecording: boolean;
    durationMillis: number;
    url: string | null;
};

const mockAudioRecorder = {
    prepareToRecordAsync: jest.fn(async () => undefined),
    record: jest.fn(),
    stop: jest.fn(async () => undefined),
    uri: "file://voice.m4a",
    isRecording: false,
};
let mockRecorderState: MockRecorderState = {
    isRecording: false,
    durationMillis: 0,
    url: null,
};

jest.mock("expo-audio", () => ({
    AudioModule: {
        requestRecordingPermissionsAsync: jest.fn(async () => ({ granted: true })),
    },
    RecordingPresets: {
        HIGH_QUALITY: {
            android: {},
            ios: {},
            web: {},
        },
    },
    setAudioModeAsync: jest.fn(async () => undefined),
    useAudioRecorder: jest.fn(() => mockAudioRecorder),
    useAudioRecorderState: jest.fn(() => mockRecorderState),
}));

describe("useVoiceRecorder", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockAudioRecorder.uri = "file://voice.m4a";
        mockAudioRecorder.isRecording = false;
        mockAudioRecorder.stop.mockImplementation(async () => {
            mockAudioRecorder.isRecording = false;
            mockRecorderState = {
                ...mockRecorderState,
                isRecording: false,
            };
        });
        mockRecorderState = {
            isRecording: false,
            durationMillis: 0,
            url: null,
        };
        (AudioModule.requestRecordingPermissionsAsync as jest.Mock).mockResolvedValue({
            granted: true,
        });
    });

    it("requests permission and enables recording mode only while starting", async () => {
        const { result } = renderHook(() => useVoiceRecorder());

        await act(async () => {
            await result.current.startRecording();
        });

        expect(AudioModule.requestRecordingPermissionsAsync).toHaveBeenCalledTimes(1);
        expect(setAudioModeAsync).toHaveBeenCalledWith(
            expect.objectContaining({ allowsRecording: true, playsInSilentMode: true }),
        );
        expect(mockAudioRecorder.prepareToRecordAsync).toHaveBeenCalledTimes(1);
        expect(mockAudioRecorder.record).toHaveBeenCalledTimes(1);
    });

    it("keeps recorder idle and exposes an error when microphone permission is denied", async () => {
        (AudioModule.requestRecordingPermissionsAsync as jest.Mock).mockResolvedValue({
            granted: false,
        });
        const { result } = renderHook(() => useVoiceRecorder());

        await act(async () => {
            await result.current.startRecording();
        });

        expect(mockAudioRecorder.prepareToRecordAsync).not.toHaveBeenCalled();
        expect(mockAudioRecorder.record).not.toHaveBeenCalled();
        expect(setAudioModeAsync).not.toHaveBeenCalledWith(
            expect.objectContaining({ allowsRecording: true }),
        );
        expect(result.current.error).toContain("микрофону");
    });

    it("creates a voice draft with whole-second duration and disables recording mode on stop", async () => {
        const { result, rerender } = renderHook(() => useVoiceRecorder());
        mockRecorderState = {
            isRecording: true,
            durationMillis: 12_300,
            url: null,
        };
        rerender(undefined);

        await act(async () => {
            await result.current.stopRecording();
        });

        expect(mockAudioRecorder.stop).toHaveBeenCalledTimes(1);
        expect(result.current.draft).toEqual(
            expect.objectContaining({
                uri: "file://voice.m4a",
                mimeType: "audio/m4a",
                durationSeconds: 12,
            }),
        );
        expect(setAudioModeAsync).toHaveBeenCalledWith(
            expect.objectContaining({ allowsRecording: false }),
        );
    });

    it("auto-stops when the maximum voice duration is reached", async () => {
        mockAudioRecorder.uri = "file://auto.webm";
        const { rerender } = renderHook(() =>
            useVoiceRecorder({ maxDurationSeconds: 5 }),
        );
        mockRecorderState = {
            isRecording: true,
            durationMillis: 5_000,
            url: null,
        };

        await act(async () => {
            rerender(undefined);
            await Promise.resolve();
        });

        expect(mockAudioRecorder.stop).toHaveBeenCalledTimes(1);
    });

    it("cancels an active recording and returns the audio mode to playback", async () => {
        const { result, rerender } = renderHook(() => useVoiceRecorder());
        mockRecorderState = {
            isRecording: true,
            durationMillis: 1_000,
            url: null,
        };
        rerender(undefined);

        await act(async () => {
            await result.current.cancelRecording();
        });

        expect(mockAudioRecorder.stop).toHaveBeenCalledTimes(1);
        expect(result.current.draft).toBeNull();
        expect(setAudioModeAsync).toHaveBeenCalledWith(
            expect.objectContaining({ allowsRecording: false }),
        );
    });
});
