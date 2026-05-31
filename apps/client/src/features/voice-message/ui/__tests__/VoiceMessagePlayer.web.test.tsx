import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { apiClient } from "@/shared/lib/api";
import { VoiceMessagePlayer } from "../VoiceMessagePlayer.web";

class MockWebAudio {
    preload = "";
    src = "";
    currentTime = 0;
    duration = 20;
    play = jest.fn(async () => {
        this.dispatch("play");
    });
    pause = jest.fn(() => {
        this.dispatch("pause");
    });
    load = jest.fn(() => {
        this.dispatch("loadedmetadata");
    });
    private listeners: Record<string, Set<() => void>> = {};

    addEventListener(type: string, listener: () => void) {
        this.listeners[type] ??= new Set();
        this.listeners[type].add(listener);
    }

    removeEventListener(type: string, listener: () => void) {
        this.listeners[type]?.delete(listener);
    }

    getAttribute(name: string) {
        return name === "src" && this.src ? this.src : null;
    }

    removeAttribute(name: string) {
        if (name === "src") {
            this.src = "";
        }
    }

    private dispatch(type: string) {
        this.listeners[type]?.forEach((listener) => listener());
    }
}

const mockAudios: MockWebAudio[] = [];

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        media: {
            getAssetDownloadUrl: jest.fn(),
        },
    },
}));

jest.mock("@/shared/ui/activity-indicator", () => ({
    ActivityIndicator: ({ testID }: { testID?: string }) => {
        const { Text } = require("react-native");
        return <Text testID={testID ?? "activity-indicator"}>loading</Text>;
    },
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        DownloadSimpleIcon: makeIcon("download-icon"),
        PauseIcon: makeIcon("pause-icon"),
        PlayIcon: makeIcon("play-icon"),
        WarningCircleIcon: makeIcon("warning-icon"),
    };
});

const getStyleHeight = (node: any) => {
    const style = node.props.style;
    if (Array.isArray(style)) {
        return Object.assign({}, ...style).height;
    }
    return style?.height;
};

describe("VoiceMessagePlayer web", () => {
    const originalAudio = (globalThis as any).Audio;
    const originalFetch = (globalThis as any).fetch;
    const originalAudioContext = (globalThis as any).AudioContext;

    beforeEach(() => {
        jest.clearAllMocks();
        mockAudios.length = 0;
        Object.defineProperty(globalThis, "Audio", {
            configurable: true,
            value: jest.fn(() => {
                const audio = new MockWebAudio();
                mockAudios.push(audio);
                return audio;
            }),
        });
        (apiClient.media.getAssetDownloadUrl as jest.Mock).mockResolvedValue({
            downloadUrl: "https://media.example/voice.m4a",
        });
    });

    afterEach(() => {
        Object.defineProperty(globalThis, "Audio", {
            configurable: true,
            value: originalAudio,
        });
        Object.defineProperty(globalThis, "fetch", {
            configurable: true,
            value: originalFetch,
        });
        Object.defineProperty(globalThis, "AudioContext", {
            configurable: true,
            value: originalAudioContext,
        });
    });

    it("seeks from web timeline offset coordinates", async () => {
        render(
            <VoiceMessagePlayer
                sourceUri="https://media.example/voice.m4a"
                durationSeconds={20}
            />,
        );
        const timeline = screen.getByTestId("voice-message-timeline");

        fireEvent(timeline, "layout", {
            nativeEvent: { layout: { width: 200 } },
        });
        await act(async () => {
            fireEvent.press(timeline, { nativeEvent: { offsetX: 150 } });
        });

        expect(mockAudios[0].currentTime).toBe(15);
    });

    it("seeks from DOM client coordinates when layout width is not ready", async () => {
        render(
            <VoiceMessagePlayer
                sourceUri="https://media.example/voice.m4a"
                durationSeconds={20}
            />,
        );
        const timeline = screen.getByTestId("voice-message-timeline");

        await act(async () => {
            fireEvent.press(timeline, {
                currentTarget: {
                    getBoundingClientRect: () => ({ left: 10, width: 200 }),
                },
                nativeEvent: { clientX: 60 },
            });
        });

        expect(mockAudios[0].currentTime).toBe(5);
    });

    it("resolves remote media on mount so remote voice waveforms can be decoded", async () => {
        render(<VoiceMessagePlayer mediaAssetId="asset-1" durationSeconds={20} />);

        await waitFor(() => {
            expect(apiClient.media.getAssetDownloadUrl).toHaveBeenCalledWith("asset-1");
            expect(screen.getByTestId("play-icon")).toBeTruthy();
        });
    });

    it("draws waveform bars from decoded audio samples when available", async () => {
        const samples = new Float32Array(3700);
        samples.fill(0.1, 0, samples.length / 2);
        samples.fill(0.9, samples.length / 2);
        const decodedAudio = {
            length: samples.length,
            numberOfChannels: 1,
            getChannelData: jest.fn(() => samples),
        };

        Object.defineProperty(globalThis, "fetch", {
            configurable: true,
            value: jest.fn(async () => ({
                arrayBuffer: jest.fn(async () => new ArrayBuffer(8)),
            })),
        });
        Object.defineProperty(globalThis, "AudioContext", {
            configurable: true,
            value: class {
                decodeAudioData(
                    _data: ArrayBuffer,
                    onSuccess?: (buffer: typeof decodedAudio) => void,
                ) {
                    onSuccess?.(decodedAudio);
                    return Promise.resolve(decodedAudio);
                }

                close() {
                    return Promise.resolve();
                }
            },
        });

        render(
            <VoiceMessagePlayer
                sourceUri="https://media.example/voice.m4a"
                durationSeconds={20}
            />,
        );

        await waitFor(() => {
            const heights = screen
                .getAllByTestId("voice-message-waveform-bar")
                .slice(0, 37)
                .map(getStyleHeight);

            expect(Math.max(...heights)).toBeGreaterThan(25);
            expect(Math.min(...heights)).toBeLessThan(16);
        });
    });
});
