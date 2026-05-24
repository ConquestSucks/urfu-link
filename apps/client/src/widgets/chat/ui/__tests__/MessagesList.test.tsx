import React from "react";
import { act, fireEvent, render, screen } from "@testing-library/react-native";

import { MessagesList, type MessagesListHandle } from "../MessagesList";

const mockLoadMessages = jest.fn();
const mockLoadMore = jest.fn();
const mockMarkRead = jest.fn();
const mockAddReaction = jest.fn();
const mockRemoveReaction = jest.fn();
const mockTypingIndicator = jest.fn();

let mockChatState = {
    messagesByConversation: {},
    messagesLoadingByConversation: {},
    messagesLoadedByConversation: {},
    hasMoreByConversation: {},
    isLoading: false,
    loadMessages: mockLoadMessages,
    loadMore: mockLoadMore,
    markRead: mockMarkRead,
    addReaction: mockAddReaction,
    removeReaction: mockRemoveReaction,
};

jest.mock("@/entities/conversation/model/chat-store", () => ({
    mapMessageToProps: (message: { id: string; body: string }) => ({
        id: message.id,
        text: message.body,
        isOwn: false,
        time: "12:00",
        avatarUrl: "",
    }),
    useChatStore: (selector?: (state: typeof mockChatState) => unknown) =>
        selector ? selector(mockChatState) : mockChatState,
}));

jest.mock("@/entities/chat-message", () => ({
    ChatMessage: ({
        id,
        text,
        isHighlighted,
        replyTo,
        onReplyPress,
    }: {
        id: string;
        text: string;
        isHighlighted?: boolean;
        replyTo?: { messageId: string; preview: string } | null;
        onReplyPress?: () => void;
    }) => {
        const { Text } = require("react-native");
        return (
            <>
                <Text testID={`message-${id}`}>
                    {text}:{isHighlighted ? "highlighted" : "plain"}
                </Text>
                {replyTo ? (
                    <Text testID={`reply-preview-${id}`} onPress={onReplyPress}>
                        {replyTo.preview}
                    </Text>
                ) : null}
            </>
        );
    },
}));

jest.mock("@/entities/chat-message/ui/ChatMessageSkeleton", () => ({
    ChatMessageSkeleton: () => {
        const { Text } = require("react-native");
        return <Text testID="message-skeleton">message skeleton</Text>;
    },
}));

jest.mock("@/shared/ui/activity-indicator", () => ({
    ActivityIndicator: () => {
        const { Text } = require("react-native");
        return <Text testID="loading-more">loading more</Text>;
    },
}));

jest.mock("@/shared/ui", () => ({
    EmptyState: () => {
        const { Text } = require("react-native");
        return <Text testID="empty-state">empty</Text>;
    },
}));

jest.mock("@/shared/ui/phosphor", () => ({
    ChatCircleTextIcon: () => null,
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "current-user",
}));

jest.mock("@/entities/presence", () => ({
    TypingIndicator: (props: {
        conversationId: string;
        excludeUserId?: string | null;
        showNames?: boolean;
        variant?: string;
    }) => {
        mockTypingIndicator(props);
        const { Text } = require("react-native");
        return <Text testID="dialog-typing-indicator">typing</Text>;
    },
}));

describe("MessagesList loading state", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockChatState = {
            messagesByConversation: {},
            messagesLoadingByConversation: {},
            messagesLoadedByConversation: {},
            hasMoreByConversation: {},
            isLoading: false,
            loadMessages: mockLoadMessages,
            loadMore: mockLoadMore,
            markRead: mockMarkRead,
            addReaction: mockAddReaction,
            removeReaction: mockRemoveReaction,
        };
    });

    it("shows the empty state instead of skeletons for an already loaded empty chat", () => {
        mockChatState = {
            ...mockChatState,
            messagesByConversation: { "chat-1": [] },
            messagesLoadingByConversation: { "chat-1": true },
            messagesLoadedByConversation: { "chat-1": true },
            isLoading: true,
        };

        render(<MessagesList chatId="chat-1" type="chat" />);

        expect(screen.getByTestId("empty-state")).toBeTruthy();
        expect(screen.queryByTestId("message-skeleton")).toBeNull();
    });

    it("still shows skeletons for a first load of an empty chat", () => {
        mockChatState = {
            ...mockChatState,
            messagesByConversation: { "chat-2": [] },
            messagesLoadingByConversation: { "chat-2": true },
            messagesLoadedByConversation: { "chat-2": false },
            isLoading: true,
        };

        render(<MessagesList chatId="chat-2" type="chat" />);

        expect(screen.getAllByTestId("message-skeleton")).toHaveLength(5);
    });

    it("does not request messages for an empty direct draft", () => {
        render(<MessagesList chatId="draft-1" type="chat" skipInitialLoad />);

        expect(mockLoadMessages).not.toHaveBeenCalled();
        expect(screen.getByTestId("empty-state")).toBeTruthy();
    });

    it("renders the peer typing indicator inside the dialog", () => {
        mockChatState = {
            ...mockChatState,
            messagesByConversation: {
                "chat-3": [
                    {
                        id: "message-1",
                        body: "Привет",
                        senderId: "peer-user",
                        readAt: null,
                    },
                ],
            },
            messagesLoadedByConversation: { "chat-3": true },
        };

        render(<MessagesList chatId="chat-3" type="chat" />);

        expect(screen.getByTestId("dialog-typing-indicator")).toBeTruthy();
        expect(mockTypingIndicator).toHaveBeenCalledWith({
            conversationId: "chat-3",
            excludeUserId: "current-user",
            showNames: false,
            variant: "bubble",
        });
    });

    it("renders the sticky date label and inline date separators", () => {
        jest.useFakeTimers();
        jest.setSystemTime(new Date("2026-05-24T12:00:00.000Z"));
        mockChatState = {
            ...mockChatState,
            messagesByConversation: {
                "chat-dates": [
                    {
                        id: "message-today",
                        body: "today",
                        senderId: "peer-user",
                        readAt: null,
                        createdAt: "2026-05-24T10:00:00.000Z",
                    },
                    {
                        id: "message-yesterday",
                        body: "yesterday",
                        senderId: "peer-user",
                        readAt: null,
                        createdAt: "2026-05-23T10:00:00.000Z",
                    },
                ],
            },
            messagesLoadedByConversation: { "chat-dates": true },
        };

        render(<MessagesList chatId="chat-dates" type="chat" />);

        expect(screen.getAllByText("Сегодня").length).toBeGreaterThan(0);
        expect(screen.getByText(/мая/)).toBeTruthy();
        jest.useRealTimers();
    });

    it("highlights a message for a few seconds after imperative scroll", async () => {
        jest.useFakeTimers();
        mockChatState = {
            ...mockChatState,
            messagesByConversation: {
                "chat-4": [
                    {
                        id: "message-1",
                        body: "Привет",
                        senderId: "peer-user",
                        readAt: null,
                    },
                ],
            },
            messagesLoadedByConversation: { "chat-4": true },
        };

        const ref = React.createRef<MessagesListHandle>();
        render(<MessagesList ref={ref} chatId="chat-4" type="chat" />);

        await act(async () => {
            await expect(ref.current!.scrollToMessage("message-1")).resolves.toBe(true);
        });

        expect(screen.getByTestId("message-message-1").props.children).toEqual([
            "Привет",
            ":",
            "highlighted",
        ]);
        expect(screen.getByTestId("message-highlight-message-1")).toBeTruthy();
        expect(screen.getByTestId("message-row-message-1").props.className).not.toContain(
            "bg-brand-500/15",
        );

        act(() => {
            jest.runOnlyPendingTimers();
        });

        expect(screen.getByTestId("message-message-1").props.children).toEqual([
            "Привет",
            ":",
            "plain",
        ]);
        expect(screen.queryByTestId("message-highlight-message-1")).toBeNull();
        jest.useRealTimers();
    });

    it("scrolls to and highlights the original message from a reply preview", async () => {
        jest.useFakeTimers();
        mockChatState = {
            ...mockChatState,
            messagesByConversation: {
                "chat-replies": [
                    {
                        id: "reply-message",
                        body: "reply",
                        senderId: "peer-user",
                        readAt: null,
                        replyTo: {
                            messageId: "original-message",
                            senderId: "current-user",
                            preview: "original",
                        },
                    },
                    {
                        id: "original-message",
                        body: "original",
                        senderId: "current-user",
                        readAt: null,
                    },
                ],
            },
            messagesLoadedByConversation: { "chat-replies": true },
        };

        render(<MessagesList chatId="chat-replies" type="chat" />);

        await act(async () => {
            fireEvent.press(screen.getByTestId("reply-preview-reply-message"));
        });

        expect(screen.getByTestId("message-original-message").props.children).toEqual([
            "original",
            ":",
            "highlighted",
        ]);
        expect(screen.getByTestId("message-highlight-original-message")).toBeTruthy();
        jest.useRealTimers();
    });
});
