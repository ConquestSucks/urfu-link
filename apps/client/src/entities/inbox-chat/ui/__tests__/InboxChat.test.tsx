import { act, render, screen } from "@testing-library/react-native";

import { InboxChat } from "../InboxChat";

jest.mock("@/shared/ui", () => ({
    Avatar: () => {
        const { Text } = require("react-native");
        return <Text testID="avatar">avatar</Text>;
    },
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        CheckIcon: makeIcon("single-check"),
        ChecksIcon: makeIcon("double-check"),
    };
});

describe("InboxChat unread preview", () => {
    it("highlights an unread incoming latest message", () => {
        render(
            <InboxChat
                id="direct-1"
                avatarUrl=""
                name="Peer User"
                message="new incoming"
                time="12:00"
                unreadCount={2}
                lastMessageFromSelf={false}
            />,
        );

        expect(screen.getByText("2")).toBeTruthy();
        expect(screen.getByText("new incoming").props.className).toContain("text-white");
        expect(screen.getByText("12:00").props.className).toContain("text-brand-300");
    });

    it("renders outgoing read checks after the last message preview", () => {
        const { toJSON } = render(
            <InboxChat
                id="direct-1"
                avatarUrl=""
                name="Peer User"
                message="read outgoing"
                time="12:00"
                lastMessageFromSelf
                lastMessageRead
            />,
        );

        const serialized = JSON.stringify(toJSON());

        expect(serialized.indexOf("read outgoing")).toBeLessThan(
            serialized.indexOf("double-check"),
        );
    });

    it("renders an animated typing preview instead of message checks", () => {
        jest.useFakeTimers();
        const { unmount } = render(
            <InboxChat
                id="direct-1"
                avatarUrl=""
                name="Peer User"
                message="Печатает"
                time="12:00"
                isTyping
                lastMessageFromSelf
                lastMessageRead
            />,
        );

        try {
            expect(screen.getByTestId("inbox-typing-preview")).toBeTruthy();
            expect(screen.getByTestId("inbox-typing-dots").props.children).toHaveLength(3);
            expect(screen.getByTestId("inbox-typing-dots").props.children[0].props.className).toContain("opacity-100");
            act(() => {
                jest.advanceTimersByTime(350);
            });
            expect(screen.getByTestId("inbox-typing-dots").props.children[1].props.className).toContain("opacity-100");
            expect(screen.getByText("Печатает").props.className).toContain("text-brand-300");
            expect(screen.queryByTestId("double-check")).toBeNull();
            expect(screen.queryByTestId("single-check")).toBeNull();
        } finally {
            unmount();
            jest.useRealTimers();
        }
    });
});
