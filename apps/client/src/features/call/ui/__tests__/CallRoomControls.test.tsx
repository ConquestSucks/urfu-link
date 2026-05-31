import { fireEvent, render, screen } from "@testing-library/react-native";

jest.mock("react-native-reanimated", () => {
    const { Text, View } = require("react-native");
    const animationBuilder = {
        delay: () => animationBuilder,
        duration: () => animationBuilder,
        springify: () => animationBuilder,
    };

    return {
        __esModule: true,
        default: {
            Text,
            View,
            createAnimatedComponent: (Component: React.ComponentType) => Component,
        },
        FadeIn: animationBuilder,
        FadeInDown: animationBuilder,
        FadeInRight: animationBuilder,
        FadeOut: animationBuilder,
        LinearTransition: animationBuilder,
        ZoomIn: animationBuilder,
        ZoomOut: animationBuilder,
    };
});

import {
    CallControls,
    ParticipantStatusIcons,
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
    it("renders media controls in the bottom bar", () => {
        const onToggleCamera = jest.fn();
        const onToggleScreenShare = jest.fn();

        render(
            <CallControls
                micEnabled
                cameraEnabled={false}
                screenShareEnabled={false}
                switchCameraAvailable={false}
                screenShareAvailable
                busy={false}
                isMobile={false}
                onToggleMicrophone={jest.fn()}
                onToggleCamera={onToggleCamera}
                onSwitchCamera={jest.fn()}
                onToggleScreenShare={onToggleScreenShare}
                onOpenChat={jest.fn()}
                onOpenParticipants={jest.fn()}
                onLeave={jest.fn()}
            />,
        );

        expect(screen.getByTestId("call-control-mic")).toBeTruthy();
        expect(screen.getByTestId("call-control-camera")).toBeTruthy();
        expect(screen.getByTestId("call-control-screen")).toBeTruthy();
        expect(screen.getByTestId("call-control-chat")).toBeTruthy();
        expect(screen.getByTestId("call-control-more")).toBeTruthy();
        expect(screen.getByTestId("call-control-leave")).toBeTruthy();
        expect(screen.queryByTestId("call-control-switch-camera")).toBeNull();
        expect(screen.getByText("Камера")).toBeTruthy();
        expect(screen.getByText("Экран")).toBeTruthy();
        expect(screen.queryByText("Сменить камеру")).toBeNull();

        fireEvent.press(screen.getByTestId("call-control-camera"));
        fireEvent.press(screen.getByTestId("call-control-screen"));

        expect(onToggleCamera).toHaveBeenCalledTimes(1);
        expect(onToggleScreenShare).toHaveBeenCalledTimes(1);
    });

    it("renders switch camera only for mobile controls", () => {
        render(
            <CallControls
                micEnabled
                cameraEnabled
                screenShareEnabled={false}
                switchCameraAvailable
                screenShareAvailable
                busy={false}
                isMobile
                onToggleMicrophone={jest.fn()}
                onToggleCamera={jest.fn()}
                onSwitchCamera={jest.fn()}
                onToggleScreenShare={jest.fn()}
                onOpenChat={jest.fn()}
                onOpenParticipants={jest.fn()}
                onLeave={jest.fn()}
            />,
        );

        expect(screen.getByTestId("call-control-switch-camera")).toBeTruthy();
        expect(screen.queryByText("Камера")).toBeNull();
        expect(screen.queryByText("Сменить камеру")).toBeNull();
        expect(screen.queryByText("Экран")).toBeNull();
    });

    it("uses an adaptive icon-only wrapped dock on mobile", () => {
        render(
            <CallControls
                micEnabled
                cameraEnabled
                screenShareEnabled={false}
                switchCameraAvailable
                screenShareAvailable
                busy={false}
                isMobile
                onToggleMicrophone={jest.fn()}
                onToggleCamera={jest.fn()}
                onSwitchCamera={jest.fn()}
                onToggleScreenShare={jest.fn()}
                onOpenChat={jest.fn()}
                onOpenParticipants={jest.fn()}
                onLeave={jest.fn()}
            />,
        );

        expect(screen.getByTestId("call-controls-dock").props.style).toEqual(
            expect.objectContaining({ flexWrap: "wrap" }),
        );
        expect(screen.getByTestId("call-control-mic").props.style).toEqual(
            expect.objectContaining({ width: 44, height: 44 }),
        );
        expect(screen.queryByText("Микрофон")).toBeNull();
        expect(screen.queryByText("Камера")).toBeNull();
        expect(screen.queryByText("Экран")).toBeNull();
    });

    it("renders participant media states as icons without long compact text badges", () => {
        render(
            <ParticipantStatusIcons
                isConnected={false}
                isMicrophoneEnabled={false}
                isCameraEnabled={false}
                isScreenShareEnabled
                showCamera
            />,
        );

        expect(screen.getByLabelText("Участник подключается")).toBeTruthy();
        expect(screen.getByLabelText("Микрофон выключен")).toBeTruthy();
        expect(screen.getByLabelText("Камера выключена")).toBeTruthy();
        expect(screen.getByLabelText("Демонстрация экрана")).toBeTruthy();
        expect(screen.queryByText("Микрофон выключен")).toBeNull();
        expect(screen.queryByText("Камера выключена")).toBeNull();
        expect(screen.queryByText("Демонстрация экрана")).toBeNull();
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

        expect(screen.queryByText("Говорит")).toBeNull();
        expect(screen.getByLabelText("Сейчас говорит")).toBeTruthy();
    });
});
