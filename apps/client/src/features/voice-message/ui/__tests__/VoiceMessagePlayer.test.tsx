import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { setAudioModeAsync, useAudioPlayer } from "expo-audio";
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
        DownloadSimpleIcon: makeIcon("download-icon"),
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

    it("downloads a remote voice before showing playback controls", async () => {
        render(
            <VoiceMessagePlayer
                mediaAssetId="asset-1"
                durationSeconds={17}
                isOwn
            />,
        );

        expect(screen.getByTestId("download-icon")).toBeTruthy();
        expect(screen.queryByTestId("play-icon")).toBeNull();

        await act(async () => {
            fireEvent.press(screen.getByTestId("voice-message-play-button"));
        });

        await waitFor(() => {
            expect(apiClient.media.getAssetDownloadUrl).toHaveBeenCalledWith("asset-1");
            expect(mockPlayer.replace).toHaveBeenCalledWith({
                uri: "https://media.example/voice.m4a",
            });
        });
        expect(mockPlayer.play).not.toHaveBeenCalled();
        expect(setAudioModeAsync).not.toHaveBeenCalled();
        expect(screen.getByTestId("play-icon")).toBeTruthy();
        expect(screen.queryByTestId("download-icon")).toBeNull();

        await act(async () => {
            fireEvent.press(screen.getByTestId("voice-message-play-button"));
        });

        expect(apiClient.media.getAssetDownloadUrl).toHaveBeenCalledTimes(1);
        expect(mockPlayer.play).toHaveBeenCalledTimes(1);
        expect(useAudioPlayer).toHaveBeenCalledWith(null);
        expect(useAudioPlayer).not.toHaveBeenCalledWith({
            uri: "https://media.example/voice.m4a",
        });
        expect(setAudioModeAsync).toHaveBeenCalledWith(
            expect.objectContaining({ allowsRecording: false, playsInSilentMode: true }),
        );
    });

    it("shows a spinner while a remote voice is downloading", async () => {
        let resolveDownload!: (value: { downloadUrl: string }) => void;
        (apiClient.media.getAssetDownloadUrl as jest.Mock).mockReturnValue(
            new Promise((resolve) => {
                resolveDownload = resolve;
            }),
        );

        render(
            <VoiceMessagePlayer
                mediaAssetId="asset-1"
                durationSeconds={17}
            />,
        );

        await act(async () => {
            fireEvent.press(screen.getByTestId("voice-message-play-button"));
        });

        expect(screen.getByTestId("activity-indicator")).toBeTruthy();
        expect(screen.queryByTestId("download-icon")).toBeNull();
        expect(mockPlayer.play).not.toHaveBeenCalled();

        await act(async () => {
            resolveDownload({ downloadUrl: "https://media.example/voice.m4a" });
        });

        await waitFor(() => {
            expect(screen.getByTestId("play-icon")).toBeTruthy();
        });
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

    it("keeps the player stable when playing a local source URI", async () => {
        render(
            <VoiceMessagePlayer
                sourceUri="file://voice.m4a"
                durationSeconds={17}
            />,
        );
        mockPlayer.replace.mockClear();

        expect(screen.getByTestId("play-icon")).toBeTruthy();
        expect(screen.queryByTestId("download-icon")).toBeNull();

        await act(async () => {
            fireEvent.press(screen.getByTestId("voice-message-play-button"));
        });

        expect(useAudioPlayer).toHaveBeenCalledWith(null);
        expect(useAudioPlayer).not.toHaveBeenCalledWith({
            uri: "file://voice.m4a",
        });
        expect(mockPlayer.replace).not.toHaveBeenCalled();
        expect(mockPlayer.play).toHaveBeenCalledTimes(1);
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

    it("uses the loaded audio duration when saved metadata is longer", () => {
        mockStatus = {
            playing: false,
            currentTime: 4.2,
            duration: 4.2,
        };

        render(
            <VoiceMessagePlayer
                sourceUri="file://voice.m4a"
                durationSeconds={5}
            />,
        );

        expect(screen.getAllByText("0:04")).toHaveLength(2);
        expect(screen.queryByText("0:05")).toBeNull();
    });
});
