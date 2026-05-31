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
        BellIcon: makeIcon("bell-on-icon"),
        BellSlashIcon: makeIcon("bell-icon"),
        ChatCircleTextIcon: makeIcon("chat-icon"),
        EnvelopeIcon: makeIcon("mail-icon"),
        PhoneIcon: makeIcon("phone-icon"),
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
        const onClose = jest.fn();
        const onStartAudioCall = jest.fn();
        const onOpenChat = jest.fn();
        const onToggleNotifications = jest.fn();

        render(
            <UserProfileModal
                isOpen
                onClose={onClose}
                user={{ name: "Peer" }}
                onStartAudioCall={onStartAudioCall}
                onOpenChat={onOpenChat}
                onToggleNotifications={onToggleNotifications}
            />,
        );

        expect(screen.getByText("Чат")).toBeTruthy();
        expect(screen.getByTestId("chat-icon")).toBeTruthy();

        fireEvent.press(screen.getByTestId("profile-action-audio"));
        fireEvent.press(screen.getByTestId("profile-action-chat"));
        fireEvent.press(screen.getByTestId("profile-action-notifications"));

        expect(onStartAudioCall).toHaveBeenCalledTimes(1);
        expect(onOpenChat).toHaveBeenCalledTimes(1);
        expect(onToggleNotifications).toHaveBeenCalledTimes(1);
        expect(onClose).toHaveBeenCalledTimes(2);
    });

    it("renders the notification action from the shared muted state", () => {
        const { rerender } = render(
            <UserProfileModal
                isOpen
                onClose={jest.fn()}
                user={{ name: "Peer" }}
                onToggleNotifications={jest.fn()}
                notificationsMuted={false}
            />,
        );

        expect(screen.getByText("Отключить")).toBeTruthy();
        expect(screen.getByTestId("bell-icon")).toBeTruthy();

        rerender(
            <UserProfileModal
                isOpen
                onClose={jest.fn()}
                user={{ name: "Peer" }}
                onToggleNotifications={jest.fn()}
                notificationsMuted
            />,
        );

        expect(screen.getByText("Включить")).toBeTruthy();
        expect(screen.getByTestId("bell-on-icon")).toBeTruthy();
    });
});
