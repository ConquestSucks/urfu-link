import { render, screen } from "@testing-library/react-native";
import type { ReactNode } from "react";

import { ChatHeaderActions } from "../ChatHeaderActions";

jest.mock("@/shared/ui", () => ({
    Menu: () => null,
}));

jest.mock("@/shared/ui/phosphor", () => {
    const { Text } = require("react-native");
    const Icon = ({ children }: { children?: ReactNode }) => <Text>{children}</Text>;

    return {
        DotsThreeVerticalIcon: Icon,
        MagnifyingGlassIcon: Icon,
        PhoneIcon: Icon,
        VideoCameraIcon: Icon,
    };
});

describe("ChatHeaderActions", () => {
    it("renders direct call actions without persistent background or halo", () => {
        render(
            <ChatHeaderActions
                onOpenProfile={jest.fn()}
                onOpenPinned={jest.fn()}
                onSearchPress={jest.fn()}
                onStartAudioCall={jest.fn()}
                onStartVideoCall={jest.fn()}
                notificationsMuted={false}
                onToggleNotifications={jest.fn()}
            />,
        );

        const audioButton = screen.getByTestId("chat-header-audio-call");
        const videoButton = screen.getByTestId("chat-header-video-call");

        expect(audioButton.props.className).not.toContain("bg-brand");
        expect(videoButton.props.className).not.toContain("bg-brand");
        expect(audioButton.props.style).toBeUndefined();
        expect(videoButton.props.style).toBeUndefined();
    });
});
