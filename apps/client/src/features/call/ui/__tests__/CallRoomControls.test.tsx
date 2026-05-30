import { fireEvent, render, screen } from "@testing-library/react-native";

import {
    CallControls,
    MediaOverlayControls,
    SpeakingFrame,
} from "../CallRoomControls";

jest.mock("@/shared/ui", () => ({
    Avatar: ({ name }: { name?: string }) => {
        const { Text } = require("react-native");
        return <Text testID="avatar">{name}</Text>;
    },
    ModalOverlay: ({ children, visible }: { children: React.ReactNode; visible?: boolean }) =>
        visible ? <>{children}</> : null,
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        ArrowsClockwiseIcon: makeIcon("switch-camera-icon"),
        ChatCircleTextIcon: makeIcon("chat-icon"),
        DotsThreeVerticalIcon: makeIcon("more-icon"),
        MicrophoneIcon: makeIcon("mic-icon"),
        MicrophoneSlashIcon: makeIcon("mic-off-icon"),
        PhoneDisconnectIcon: makeIcon("leave-icon"),
        ScreencastIcon: makeIcon("screen-icon"),
        VideoCameraIcon: makeIcon("camera-icon"),
        XIcon: makeIcon("close-icon"),
    };
});

jest.mock("../CallChatPanel", () => ({
    CallChatPanel: () => {
        const { Text } = require("react-native");
        return <Text testID="call-chat-panel">chat</Text>;
    },
}));

describe("CallRoomControls", () => {
    it("keeps video controls out of the bottom bar", () => {
        render(
            <CallControls
                micEnabled
                busy={false}
                isMobile={false}
                onToggleMicrophone={jest.fn()}
                onOpenChat={jest.fn()}
                onOpenParticipants={jest.fn()}
                onLeave={jest.fn()}
            />,
        );

        expect(screen.getByTestId("call-control-mic")).toBeTruthy();
        expect(screen.getByTestId("call-control-chat")).toBeTruthy();
        expect(screen.getByTestId("call-control-more")).toBeTruthy();
        expect(screen.getByTestId("call-control-leave")).toBeTruthy();
        expect(screen.queryByTestId("call-control-camera")).toBeNull();
        expect(screen.queryByTestId("call-control-switch-camera")).toBeNull();
        expect(screen.queryByTestId("call-control-screen")).toBeNull();
        expect(screen.queryByText("Камера")).toBeNull();
        expect(screen.queryByText("Сменить камеру")).toBeNull();
        expect(screen.queryByText("Экран")).toBeNull();
    });

    it("renders camera actions as icon-only overlay controls", () => {
        const onToggleCamera = jest.fn();
        const onSwitchCamera = jest.fn();
        const onToggleScreenShare = jest.fn();

        render(
            <MediaOverlayControls
                cameraEnabled
                screenShareEnabled={false}
                switchCameraAvailable
                screenShareAvailable
                busy={false}
                onToggleCamera={onToggleCamera}
                onSwitchCamera={onSwitchCamera}
                onToggleScreenShare={onToggleScreenShare}
            />,
        );

        fireEvent.press(screen.getByTestId("call-media-camera"));
        fireEvent.press(screen.getByTestId("call-media-switch-camera"));
        fireEvent.press(screen.getByTestId("call-media-screen"));

        expect(onToggleCamera).toHaveBeenCalledTimes(1);
        expect(onSwitchCamera).toHaveBeenCalledTimes(1);
        expect(onToggleScreenShare).toHaveBeenCalledTimes(1);
        expect(screen.queryByText("Камера")).toBeNull();
        expect(screen.queryByText("Сменить камеру")).toBeNull();
        expect(screen.queryByText("Экран")).toBeNull();
    });

    it("shows a speaking indicator only while participant is speaking", () => {
        const { rerender } = render(
            <SpeakingFrame isSpeaking={false}>
                <></>
            </SpeakingFrame>,
        );

        expect(screen.queryByText("Говорит")).toBeNull();

        rerender(
            <SpeakingFrame isSpeaking>
                <></>
            </SpeakingFrame>,
        );

        expect(screen.getByText("Говорит")).toBeTruthy();
    });
});
