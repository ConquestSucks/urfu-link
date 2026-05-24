import { act, fireEvent, render, screen } from "@testing-library/react-native";
import { Platform, StyleSheet } from "react-native";
import type { ReactNode } from "react";
import { ChatInput } from "../Input";
import { useTypingIndicator } from "@/shared/lib/useTypingIndicator";

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: { editMessage: jest.Mock }) => unknown) =>
        selector({ editMessage: jest.fn() }),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useConversationParticipants: () => [],
    useParticipantsStore: jest.fn(),
}));

jest.mock("@/features/attach-file", () => ({
    FilesModal: () => null,
    useAttachments: () => ({
        attachments: [],
        isFilesModalVisible: false,
        setIsFilesModalVisible: jest.fn(),
        handleAttachFiles: jest.fn(),
        removeAttachment: jest.fn(),
        clearAttachments: jest.fn(),
    }),
}));

jest.mock("@/features/emoji-picker", () => ({
    EmojiPicker: () => null,
}));

jest.mock("@/features/message-actions", () => ({
    useComposerStore: (
        selector: (state: {
            replyTo: null;
            editing: null;
            reset: jest.Mock;
            setReply: jest.Mock;
        }) => unknown,
    ) =>
        selector({
            replyTo: null,
            editing: null,
            reset: jest.fn(),
            setReply: jest.fn(),
        }),
}));

jest.mock("@/features/mentions", () => ({
    MentionSuggestions: () => null,
    findMentionAtCursor: () => null,
}));

jest.mock("@/shared/lib/useTypingIndicator", () => ({
    useTypingIndicator: jest.fn(() => ({
        onTextChange: jest.fn(),
        onSend: jest.fn(),
    })),
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "current-user",
}));

jest.mock("@/shared/ui/phosphor", () => {
    const { Text } = require("react-native");
    const Icon = ({ children }: { children?: ReactNode }) => <Text>{children}</Text>;

    return {
        PaperPlaneRightIcon: Icon,
        PencilSimpleIcon: Icon,
        PlusCircleIcon: Icon,
        SmileyIcon: Icon,
        XIcon: Icon,
    };
});

describe("ChatInput", () => {
    beforeEach(() => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
        (useTypingIndicator as jest.Mock).mockClear();
    });

    it("keeps the placeholder vertically centered and removes the browser focus outline", () => {
        render(<ChatInput conversationId="conversation-1" onSend={jest.fn()} />);

        const input = screen.getByPlaceholderText("Сообщение");
        const style = StyleSheet.flatten(input.props.style);

        expect(style.height).toBe(44);
        expect(style.lineHeight).toBe(24);
        expect(style.paddingTop).toBe(10);
        expect(style.paddingBottom).toBe(10);
        expect(style.outlineWidth).toBe(0);
    });

    it("does not expand to the stale web layout height after a line break", () => {
        render(<ChatInput conversationId="conversation-1" onSend={jest.fn()} />);

        fireEvent.changeText(screen.getByPlaceholderText("Сообщение"), "первая\nвторая");
        fireEvent(screen.getByPlaceholderText("Сообщение"), "contentSizeChange", {
            nativeEvent: { contentSize: { height: 320 } },
        });

        const input = screen.getByPlaceholderText("Сообщение");
        const style = StyleSheet.flatten(input.props.style);

        expect(style.height).toBe(68);
    });

    it("keeps the emoji button anchored to the bottom when the composer grows", () => {
        render(<ChatInput conversationId="conversation-1" onSend={jest.fn()} />);

        fireEvent.changeText(screen.getByPlaceholderText("Сообщение"), "первая\nвторая");
        fireEvent(screen.getByPlaceholderText("Сообщение"), "contentSizeChange", {
            nativeEvent: { contentSize: { height: 320 } },
        });

        const emojiButton = screen.getByTestId("chat-input-emoji-button");
        const style = StyleSheet.flatten(emojiButton.props.style);

        expect(style.alignSelf).toBe("flex-end");
    });

    it("expands immediately when an empty composer receives a line break", () => {
        render(<ChatInput conversationId="conversation-1" onSend={jest.fn()} />);

        fireEvent.changeText(screen.getByPlaceholderText("Сообщение"), "\n");

        const input = screen.getByPlaceholderText("Сообщение");
        const style = StyleSheet.flatten(input.props.style);

        expect(style.height).toBe(68);
    });

    it("passes the typing enabled flag to the typing hook", () => {
        render(
            <ChatInput
                conversationId="conversation-1"
                onSend={jest.fn()}
                typingEnabled={false}
            />,
        );

        expect(useTypingIndicator).toHaveBeenCalledWith("conversation-1", {
            enabled: false,
        });
    });

    it("shrinks back when lines are removed", () => {
        render(<ChatInput conversationId="conversation-1" onSend={jest.fn()} />);

        fireEvent.changeText(screen.getByPlaceholderText("Сообщение"), "первая\nвторая");
        fireEvent(screen.getByPlaceholderText("Сообщение"), "contentSizeChange", {
            nativeEvent: { contentSize: { height: 68 } },
        });

        fireEvent.changeText(screen.getByPlaceholderText("Сообщение"), "первая");
        fireEvent(screen.getByPlaceholderText("Сообщение"), "contentSizeChange", {
            nativeEvent: { contentSize: { height: 68 } },
        });

        const input = screen.getByPlaceholderText("Сообщение");
        const style = StyleSheet.flatten(input.props.style);

        expect(style.height).toBe(44);
    });

    it("sends the message on Enter on web", async () => {
        const onSend = jest.fn();
        const preventDefault = jest.fn();
        render(<ChatInput conversationId="conversation-1" onSend={onSend} />);

        fireEvent.changeText(screen.getByTestId("chat-input-text"), "hello");

        await act(async () => {
            fireEvent(screen.getByTestId("chat-input-text"), "keyPress", {
                nativeEvent: { key: "Enter", shiftKey: false, preventDefault },
                preventDefault,
            });
            await new Promise((resolve) => requestAnimationFrame(resolve));
        });

        expect(preventDefault).toHaveBeenCalled();
        expect(onSend).toHaveBeenCalledWith("hello", [], undefined);
    });

    it("keeps Shift+Enter available for line breaks on web", async () => {
        const onSend = jest.fn();
        const preventDefault = jest.fn();
        render(<ChatInput conversationId="conversation-1" onSend={onSend} />);

        fireEvent.changeText(screen.getByTestId("chat-input-text"), "hello");

        await act(async () => {
            fireEvent(screen.getByTestId("chat-input-text"), "keyPress", {
                nativeEvent: { key: "Enter", shiftKey: true, preventDefault },
                preventDefault,
            });
        });

        expect(preventDefault).not.toHaveBeenCalled();
        expect(onSend).not.toHaveBeenCalled();
    });
});
