import React from "react";
import { act, fireEvent, render, screen } from "@testing-library/react-native";
import { Animated, FlatList, Platform } from "react-native";

import { MessagesList, type MessagesListHandle } from "../MessagesList";

const mockLoadMessages = jest.fn();
const mockLoadMore = jest.fn();
const mockMarkRead = jest.fn();
const mockAddReaction = jest.fn();
const mockRemoveReaction = jest.fn();
const mockTypingIndicator = jest.fn();
const originalPlatformOS = Platform.OS;

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
        Object.defineProperty(Platform, "OS", { configurable: true, value: originalPlatformOS });
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

    it("renders date separators as sticky list rows", () => {
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

        const list = screen.UNSAFE_getByType(FlatList);
        expect(screen.getByTestId("message-date-separator-2026-05-24")).toBeTruthy();
        expect(screen.getByTestId("message-date-separator-2026-05-23")).toBeTruthy();
        expect(list.props.stickyHeaderIndices).toEqual([2, 4]);
        expect(list.props.invertStickyHeaders).toBe(true);
        expect(screen.getAllByText("Сегодня")).toHaveLength(1);
        expect(screen.getByText(/мая/)).toBeTruthy();
        jest.useRealTimers();
    });

    it("keeps date separators as independent list rows", () => {
        mockChatState = {
            ...mockChatState,
            messagesByConversation: {
                "chat-date-rows": [
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
            messagesLoadedByConversation: { "chat-date-rows": true },
        };

        render(<MessagesList chatId="chat-date-rows" type="chat" />);

        const list = screen.UNSAFE_getByType(FlatList);
        expect(list.props.data).toEqual([
            expect.objectContaining({ id: "message-today", type: "message" }),
            expect.objectContaining({ dayKey: "2026-05-24", type: "date" }),
            expect.objectContaining({ id: "message-yesterday", type: "message" }),
            expect.objectContaining({ dayKey: "2026-05-23", type: "date" }),
        ]);
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

    it("uses the JS animation driver for highlighted messages on web", async () => {
        jest.useFakeTimers();
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
        const timingSpy = jest.spyOn(Animated, "timing");
        mockChatState = {
            ...mockChatState,
            messagesByConversation: {
                "chat-web": [
                    {
                        id: "message-web",
                        body: "web",
                        senderId: "peer-user",
                        readAt: null,
                    },
                ],
            },
            messagesLoadedByConversation: { "chat-web": true },
        };

        const ref = React.createRef<MessagesListHandle>();
        render(<MessagesList ref={ref} chatId="chat-web" type="chat" />);

        await act(async () => {
            await expect(ref.current!.scrollToMessage("message-web")).resolves.toBe(true);
        });

        expect(timingSpy).toHaveBeenCalled();
        expect(timingSpy.mock.calls).toEqual(
            expect.arrayContaining([
                expect.arrayContaining([
                    expect.anything(),
                    expect.objectContaining({ useNativeDriver: false }),
                ]),
            ]),
        );
        expect(
            timingSpy.mock.calls.every(([, config]) => config.useNativeDriver === false),
        ).toBe(true);

        act(() => {
            jest.runOnlyPendingTimers();
        });
        timingSpy.mockRestore();
        jest.useRealTimers();
    });

    it("does not auto-load older pages immediately after an imperative scroll", async () => {
        mockChatState = {
            ...mockChatState,
            messagesByConversation: {
                "chat-pinned": [
                    {
                        id: "message-new",
                        body: "new",
                        senderId: "peer-user",
                        readAt: null,
                    },
                    {
                        id: "message-old",
                        body: "old",
                        senderId: "peer-user",
                        readAt: null,
                    },
                ],
            },
            messagesLoadedByConversation: { "chat-pinned": true },
            hasMoreByConversation: { "chat-pinned": true },
        };

        const ref = React.createRef<MessagesListHandle>();
        render(<MessagesList ref={ref} chatId="chat-pinned" type="chat" />);

        await act(async () => {
            await expect(ref.current!.scrollToMessage("message-old")).resolves.toBe(true);
        });

        const list = screen.UNSAFE_getByType(FlatList);
        act(() => {
            list.props.onEndReached?.({ distanceFromEnd: 0 });
        });
        expect(mockLoadMore).not.toHaveBeenCalled();

        act(() => {
            list.props.onScrollBeginDrag?.();
            list.props.onScroll?.({ nativeEvent: { contentOffset: { y: 128 } } });
            list.props.onScroll?.({ nativeEvent: { contentOffset: { y: 176 } } });
            list.props.onEndReached?.({ distanceFromEnd: 0 });
        });

        expect(mockLoadMore).toHaveBeenCalledWith("chat-pinned", "chat");
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
