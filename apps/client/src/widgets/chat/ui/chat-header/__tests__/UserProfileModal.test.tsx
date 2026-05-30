import { fireEvent, render, screen } from "@testing-library/react-native";
import type { ReactNode } from "react";

import { UserProfileModal } from "../UserProfileModal";

jest.mock("@/shared/ui", () => ({
    Avatar: ({ name, src }: { name?: string; src?: string | null }) => {
        const { Text } = require("react-native");
        return <Text testID="profile-avatar">{`${name ?? ""}|${src ?? ""}`}</Text>;
    },
    ModalOverlay: ({ children, visible }: { children: ReactNode; visible: boolean }) =>
        visible ? <>{children}</> : <>{children}</>,
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        BellSlashIcon: makeIcon("bell-icon"),
        EnvelopeIcon: makeIcon("mail-icon"),
        PhoneIcon: makeIcon("phone-icon"),
        VideoCameraIcon: makeIcon("video-icon"),
        XIcon: makeIcon("close-icon"),
    };
});

jest.mock("@/entities/presence", () => ({
    LastSeenLabel: ({ lastSeenAt }: { lastSeenAt: string }) => {
        const { Text } = require("react-native");
        return <Text>{lastSeenAt}</Text>;
    },
    presenceStatusToLabel: (status: string) => status,
}));

describe("UserProfileModal", () => {
    it("keeps the last user snapshot while the modal is closing", () => {
        const { rerender } = render(
            <UserProfileModal
                isOpen
                onClose={jest.fn()}
                user={{ name: "Teacher One", avatarUrl: "avatar-1", status: "Online" }}
            />,
        );

        rerender(
            <UserProfileModal
                isOpen={false}
                onClose={jest.fn()}
                user={{ name: "Участник", avatarUrl: undefined, status: undefined }}
            />,
        );

        expect(screen.getByText("Teacher One")).toBeTruthy();
        expect(screen.getByTestId("profile-avatar").props.children).toBe(
            "Teacher One|avatar-1",
        );
        expect(screen.queryByText("Участник")).toBeNull();
    });

    it("calls profile action handlers", () => {
        const onStartAudioCall = jest.fn();
        const onStartVideoCall = jest.fn();
        const onToggleNotifications = jest.fn();

        render(
            <UserProfileModal
                isOpen
                onClose={jest.fn()}
                user={{ name: "Peer" }}
                onStartAudioCall={onStartAudioCall}
                onStartVideoCall={onStartVideoCall}
                onToggleNotifications={onToggleNotifications}
            />,
        );

        fireEvent.press(screen.getByTestId("profile-action-audio"));
        fireEvent.press(screen.getByTestId("profile-action-video"));
        fireEvent.press(screen.getByTestId("profile-action-notifications"));

        expect(onStartAudioCall).toHaveBeenCalledTimes(1);
        expect(onStartVideoCall).toHaveBeenCalledTimes(1);
        expect(onToggleNotifications).toHaveBeenCalledTimes(1);
    });
});
