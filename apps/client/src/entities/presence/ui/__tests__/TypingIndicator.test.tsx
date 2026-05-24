import { act, render, screen } from "@testing-library/react-native";

import { toPresenceTypingConversationId, usePresenceStore } from "../../model/presence-store";
import { TypingIndicator } from "../TypingIndicator";

jest.mock("@microsoft/signalr", () => ({
    HubConnectionState: {
        Connected: "Connected",
        Disconnected: "Disconnected",
    },
}));

jest.mock("@/shared/lib/signalr", () => ({
    createHubConnection: jest.fn(),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    lookupParticipantName: jest.fn(),
}));

const conversationId = "d39b2933cccdd8b2812a2b8f401fb2a9d9f6abcd";
const presenceConversationId = toPresenceTypingConversationId(conversationId);

describe("TypingIndicator", () => {
    beforeEach(() => {
        usePresenceStore.setState({
            typingByConversation: {},
        });
    });

    it("does not render typing from the current user", () => {
        usePresenceStore.setState({
            typingByConversation: {
                [presenceConversationId]: [
                    {
                        conversationId: presenceConversationId,
                        userId: "current-user",
                    },
                ],
            },
        });

        render(<TypingIndicator conversationId={conversationId} excludeUserId="current-user" />);

        expect(screen.queryByTestId("typing-indicator")).toBeNull();
    });

    it("uses compact header-friendly spacing", () => {
        usePresenceStore.setState({
            typingByConversation: {
                [presenceConversationId]: [
                    {
                        conversationId: presenceConversationId,
                        userId: "peer-user",
                    },
                ],
            },
        });

        render(<TypingIndicator conversationId={conversationId} />);

        const indicator = screen.getByTestId("typing-indicator");
        expect(indicator.props.className).not.toContain("px-4");
        expect(indicator.props.className).not.toContain("py-1");

        const text = screen.getByText("Печатает");
        expect(text.props.className).toContain("leading-none");
        expect(screen.queryByText("Печатает...")).toBeNull();
        const dots = screen.getByTestId("typing-inline-dots");
        expect(dots.props.className).toContain("h-3");
        expect(dots.props.style).toEqual({ transform: [{ translateY: 1 }] });
    });

    it("animates visible status dots without native animated driver", () => {
        jest.useFakeTimers();
        usePresenceStore.setState({
            typingByConversation: {
                [presenceConversationId]: [
                    {
                        conversationId: presenceConversationId,
                        userId: "peer-user",
                    },
                ],
            },
        });

        const { unmount } = render(<TypingIndicator conversationId={conversationId} />);

        try {
            expect(screen.getByTestId("typing-inline-dots").props.children).toHaveLength(3);
            expect(screen.getByTestId("typing-inline-dots").props.children[0].props.className).toContain("opacity-100");
            expect(screen.getByTestId("typing-inline-dots").props.children[1].props.className).toContain("opacity-30");
            act(() => {
                jest.advanceTimersByTime(350);
            });
            expect(screen.getByTestId("typing-inline-dots").props.children[1].props.className).toContain("opacity-100");
            act(() => {
                jest.advanceTimersByTime(350);
            });
            expect(screen.getByTestId("typing-inline-dots").props.children[2].props.className).toContain("opacity-100");
        } finally {
            unmount();
            jest.useRealTimers();
        }
    });

    it("renders a dialog typing bubble", () => {
        usePresenceStore.setState({
            typingByConversation: {
                [presenceConversationId]: [
                    {
                        conversationId: presenceConversationId,
                        userId: "peer-user",
                    },
                ],
            },
        });

        render(<TypingIndicator conversationId={conversationId} variant="bubble" />);

        expect(screen.getByTestId("typing-indicator-bubble")).toBeTruthy();
        expect(screen.getByText("Печатает")).toBeTruthy();
        expect(screen.getByTestId("typing-bubble-dots")).toBeTruthy();
    });
});
