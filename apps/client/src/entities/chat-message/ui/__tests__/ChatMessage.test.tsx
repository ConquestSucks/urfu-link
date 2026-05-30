import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { Linking, Platform } from "react-native";

import { ChatMessage } from "../ChatMessage";

jest.mock("@/shared/ui", () => ({
    Avatar: () => {
        const { Text } = require("react-native");
        return <Text testID="avatar">avatar</Text>;
    },
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "user-1",
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        media: {
            getAssetDownloadUrl: jest.fn(),
        },
    },
}));

jest.mock("@/features/voice-message", () => ({
    VoiceMessagePlayer: ({
        mediaAssetId,
        durationSeconds,
    }: {
        mediaAssetId?: string | null;
        durationSeconds?: number | null;
    }) => {
        const { Text } = require("react-native");
        return (
            <Text testID="voice-message-player">
                voice:{mediaAssetId}:{durationSeconds}
            </Text>
        );
    },
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        ArrowBendDoubleUpRightIcon: makeIcon("forward-icon"),
        ChatsCircleIcon: makeIcon("thread-icon"),
        CheckIcon: makeIcon("single-check"),
        ChecksIcon: makeIcon("double-check"),
        ClockIcon: makeIcon("clock-icon"),
        FileIcon: makeIcon("file-icon"),
        PencilSimpleIcon: makeIcon("edit-icon"),
        PhoneIcon: makeIcon("phone-icon"),
        TrashIcon: makeIcon("trash-icon"),
        VideoCameraIcon: makeIcon("video-icon"),
        WarningCircleIcon: makeIcon("warning-icon"),
    };
});

const { apiClient } = require("@/shared/lib/api");

describe("ChatMessage read indicator", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("keeps jump highlighting outside the message bubble", () => {
        render(
            <ChatMessage
                id="message-1"
                text="hello"
                isOwn={false}
                time="12:00"
                avatarUrl=""
                isHighlighted
            />,
        );

        expect(screen.getByTestId("chat-message-bubble-message-1").props.className).not.toContain(
            "border-brand-300",
        );
    });

    it("opens the replied message when the reply preview is pressed", () => {
        const onReplyPress = jest.fn();
        render(
            <ChatMessage
                id="message-1"
                text="hello"
                isOwn={false}
                time="12:00"
                avatarUrl=""
                replyTo={{
                    messageId: "original-message",
                    senderId: "peer-user",
                    preview: "original text",
                }}
                onReplyPress={onReplyPress}
            />,
        );

        fireEvent.press(screen.getByTestId("chat-message-reply-message-1"));

        expect(onReplyPress).toHaveBeenCalledTimes(1);
    });

    it("shows one check for an own message that has not been read", () => {
        render(
            <ChatMessage
                id="message-1"
                text="hello"
                isOwn
                time="12:00"
                avatarUrl=""
                seen={false}
            />,
        );

        expect(screen.getByTestId("single-check")).toBeTruthy();
        expect(screen.queryByTestId("double-check")).toBeNull();
    });

    it("shows two checks for an own message that has been read", () => {
        render(
            <ChatMessage
                id="message-1"
                text="hello"
                isOwn
                time="12:00"
                avatarUrl=""
                seen
            />,
        );

        expect(screen.getByTestId("double-check")).toBeTruthy();
        expect(screen.queryByTestId("single-check")).toBeNull();
    });

    it("opens the context menu from a web right click", () => {
        const originalOS = Platform.OS;
        Object.defineProperty(Platform, "OS", {
            configurable: true,
            get: () => "web",
        });
        const onContextMenu = jest.fn();
        const preventDefault = jest.fn();
        const stopPropagation = jest.fn();

        render(
            <ChatMessage
                id="message-1"
                text="hello"
                isOwn
                time="12:00"
                avatarUrl=""
                onContextMenu={onContextMenu}
            />,
        );

        fireEvent(screen.getByTestId("chat-message-bubble-message-1"), "contextMenu", {
            preventDefault,
            stopPropagation,
            nativeEvent: { pageX: 120, pageY: 80 },
        });

        expect(preventDefault).toHaveBeenCalled();
        expect(stopPropagation).toHaveBeenCalled();
        expect(onContextMenu).toHaveBeenCalledWith({ x: 120, y: 80 });

        Object.defineProperty(Platform, "OS", {
            configurable: true,
            get: () => originalOS,
        });
    });

    it("resolves media attachments through the API before opening them", async () => {
        const openUrlSpy = jest.spyOn(Linking, "openURL").mockResolvedValue(undefined);
        apiClient.media.getAssetDownloadUrl.mockResolvedValue({
            downloadUrl: "http://localhost:9000/media-private/file.json?X-Amz-Signature=abc",
        });

        render(
            <ChatMessage
                id="message-1"
                text=""
                isOwn
                time="12:00"
                avatarUrl=""
                attachments={[
                    {
                        name: "file.json",
                        url: "/api/media/asset-1/download-url",
                        mediaAssetId: "asset-1",
                    },
                ]}
            />,
        );

        fireEvent.press(screen.getByText("file.json"));

        await waitFor(() => {
            expect(apiClient.media.getAssetDownloadUrl).toHaveBeenCalledWith("asset-1");
            expect(openUrlSpy).toHaveBeenCalledWith(
                "http://localhost:9000/media-private/file.json?X-Amz-Signature=abc",
            );
        });

        openUrlSpy.mockRestore();
    });

    it("renders voice attachments through the voice player instead of file opener", () => {
        render(
            <ChatMessage
                id="message-1"
                text=""
                isOwn
                time="12:00"
                avatarUrl=""
                attachments={[
                    {
                        name: "voice.m4a",
                        url: "/api/media/asset-1/download-url",
                        mediaAssetId: "asset-1",
                        type: "Voice",
                        mimeType: "audio/m4a",
                        durationSeconds: 17,
                    },
                ]}
            />,
        );

        expect(screen.getByTestId("voice-message-player")).toBeTruthy();
        expect(screen.getByText("voice:asset-1:17")).toBeTruthy();
        expect(screen.queryByText("voice.m4a")).toBeNull();
    });

    it("uses a browser download link for media attachments on web", async () => {
        const originalOS = Platform.OS;
        Object.defineProperty(Platform, "OS", {
            configurable: true,
            get: () => "web",
        });
        const openUrlSpy = jest.spyOn(Linking, "openURL").mockResolvedValue(undefined);
        const originalDocument = (globalThis as any).document;
        const anchor = {
            style: {},
            click: jest.fn(),
            remove: jest.fn(),
        };
        const appendChild = jest.fn();
        Object.defineProperty(globalThis, "document", {
            configurable: true,
            value: {
                createElement: jest.fn(() => anchor),
                body: { appendChild },
            },
        });
        apiClient.media.getAssetDownloadUrl.mockResolvedValue({
            downloadUrl: "http://localhost:9000/media-private/file.json?X-Amz-Signature=abc",
        });

        render(
            <ChatMessage
                id="message-1"
                text=""
                isOwn
                time="12:00"
                avatarUrl=""
                attachments={[
                    {
                        name: "file.json",
                        url: "/api/media/asset-1/download-url",
                        mediaAssetId: "asset-1",
                    },
                ]}
            />,
        );

        fireEvent.press(screen.getByText("file.json"));

        await waitFor(() => {
            expect(anchor.click).toHaveBeenCalledTimes(1);
        });

        expect(appendChild).toHaveBeenCalledWith(anchor);
        expect((anchor as any).href).toBe(
            "http://localhost:9000/media-private/file.json?X-Amz-Signature=abc",
        );
        expect((anchor as any).download).toBe("file.json");
        expect((anchor as any).target).toBe("_blank");
        expect(anchor.remove).toHaveBeenCalledTimes(1);
        expect(openUrlSpy).not.toHaveBeenCalled();

        openUrlSpy.mockRestore();
        Object.defineProperty(globalThis, "document", {
            configurable: true,
            value: originalDocument,
        });
        Object.defineProperty(Platform, "OS", {
            configurable: true,
            get: () => originalOS,
        });
    });

    it.each([
        ["Started", "Audio", "Звонок", null],
        ["Started", "Video", "Видеозвонок", null],
        ["Missed", "Audio", "Пропущенный звонок", null],
        ["Declined", "Audio", "Звонок отклонён", null],
        ["Cancelled", "Audio", "Звонок отменён", null],
        ["Failed", "Audio", "Звонок завершён", null],
        ["Completed", "Audio", "Звонок завершён • 3:12", "PT3M12S"],
        ["Completed", "Video", "Звонок завершён • 1:02:03", "PT1H2M3S"],
    ] as const)("renders %s %s system call label", (status, callType, label, duration) => {
        render(
            <ChatMessage
                id="message-1"
                text=""
                kind="SystemCall"
                isOwn={false}
                time="12:00"
                avatarUrl=""
                systemCall={{
                    callId: "call-1",
                    callType,
                    status,
                    callerId: "user-1",
                    duration,
                    endReason: null,
                }}
            />,
        );

        expect(screen.getByText(label)).toBeTruthy();
    });

    it("shows started system call time without a prefix", () => {
        render(
            <ChatMessage
                id="message-1"
                text=""
                kind="SystemCall"
                isOwn={false}
                time="12:00"
                avatarUrl=""
                systemCall={{
                    callId: "call-1",
                    callType: "Audio",
                    status: "Started",
                    callerId: "user-1",
                    duration: null,
                    endReason: null,
                }}
            />,
        );

        expect(screen.getByText("12:00")).toBeTruthy();
        expect(screen.queryByText("Начало: 12:00")).toBeNull();
        expect(screen.queryByText("Завершён: 12:00")).toBeNull();
    });

    it("shows completed system call end time and duration", () => {
        render(
            <ChatMessage
                id="message-1"
                text=""
                kind="SystemCall"
                isOwn={false}
                time="12:00"
                avatarUrl=""
                systemCall={{
                    callId: "call-1",
                    callType: "Audio",
                    status: "Completed",
                    callerId: "user-1",
                    duration: "PT3M12S",
                    endReason: "Completed",
                }}
            />,
        );

        expect(screen.getByText("Завершён: 12:00")).toBeTruthy();
        expect(screen.queryByText("Начало: 12:00")).toBeNull();
        expect(screen.getByText("Длительность: 3:12")).toBeTruthy();
    });
});
