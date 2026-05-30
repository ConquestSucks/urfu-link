import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { setAudioModeAsync } from "expo-audio";
import { apiClient } from "@/shared/lib/api";
import { VoiceMessagePlayer } from "../VoiceMessagePlayer";

const mockPlayer = {
    replace: jest.fn(),
    play: jest.fn(),
    pause: jest.fn(),
    seekTo: jest.fn(async () => undefined),
};
let mockStatus = {
    playing: false,
    currentTime: 0,
    duration: 0,
};

jest.mock("expo-audio", () => ({
    setAudioModeAsync: jest.fn(async () => undefined),
    useAudioPlayer: jest.fn(() => mockPlayer),
    useAudioPlayerStatus: jest.fn(() => mockStatus),
}));

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
        PauseIcon: makeIcon("pause-icon"),
        PlayIcon: makeIcon("play-icon"),
        WarningCircleIcon: makeIcon("warning-icon"),
    };
});

describe("VoiceMessagePlayer", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockStatus = {
            playing: false,
            currentTime: 0,
            duration: 0,
        };
        (apiClient.media.getAssetDownloadUrl as jest.Mock).mockResolvedValue({
            downloadUrl: "https://media.example/voice.m4a",
        });
    });

    it("resolves media URL lazily and starts playback", async () => {
        render(
            <VoiceMessagePlayer
                mediaAssetId="asset-1"
                durationSeconds={17}
                isOwn
            />,
        );

        await act(async () => {
            fireEvent.press(screen.getByTestId("voice-message-play-button"));
        });

        await waitFor(() => {
            expect(apiClient.media.getAssetDownloadUrl).toHaveBeenCalledWith("asset-1");
            expect(mockPlayer.replace).toHaveBeenCalledWith({
                uri: "https://media.example/voice.m4a",
            });
            expect(mockPlayer.play).toHaveBeenCalledTimes(1);
        });
        expect(setAudioModeAsync).toHaveBeenCalledWith(
            expect.objectContaining({ allowsRecording: false, playsInSilentMode: true }),
        );
    });

    it("pauses without resolving a new URL when already playing", async () => {
        mockStatus = {
            playing: true,
            currentTime: 5,
            duration: 17,
        };

        render(
            <VoiceMessagePlayer
                sourceUri="file://voice.m4a"
                durationSeconds={17}
            />,
        );

        await act(async () => {
            fireEvent.press(screen.getByTestId("voice-message-play-button"));
        });

        expect(mockPlayer.pause).toHaveBeenCalledTimes(1);
        expect(apiClient.media.getAssetDownloadUrl).not.toHaveBeenCalled();
    });

    it("renders current and total voice duration", () => {
        mockStatus = {
            playing: false,
            currentTime: 5,
            duration: 20,
        };

        render(
            <VoiceMessagePlayer
                sourceUri="file://voice.m4a"
                durationSeconds={17}
            />,
        );

        expect(screen.getByText("0:05")).toBeTruthy();
        expect(screen.getByText("0:17")).toBeTruthy();
    });
});
