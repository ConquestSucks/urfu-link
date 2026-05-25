import { act, fireEvent, render, screen } from "@testing-library/react-native";
import { Platform, Pressable, StyleSheet, Text } from "react-native";
import type { ReactElement, ReactNode } from "react";
import { ChatInput } from "../Input";
import { useTypingIndicator } from "@/shared/lib/useTypingIndicator";
import type { DocumentPickerAsset } from "expo-document-picker";

let mockAttachments: DocumentPickerAsset[] = [];
const mockSetIsFilesModalVisible = jest.fn();
const mockAddAttachments = jest.fn();
const mockHandleAttachFiles = jest.fn();
const mockRemoveAttachment = jest.fn();
const mockClearAttachments = jest.fn();

let mockConversationParticipants: Array<{
    userId: string;
    role: string;
    displayName: string;
    avatarUrl: string;
}> = [];
let mockMentionToken: { start: number; end: number; query: string } | null = null;
let mockMentionSuggestions = ({
    items,
    onSelect,
}: {
    items: Array<{ userId: string; displayName: string }>;
    onSelect: (item: { userId: string; displayName: string }) => void;
}) => null as ReactElement | null;

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: { editMessage: jest.Mock }) => unknown) =>
        selector({ editMessage: jest.fn() }),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useConversationParticipants: () => mockConversationParticipants,
    useParticipantsStore: jest.fn(),
}));

jest.mock("@/features/attach-file", () => {
    const { Pressable, Text } = require("react-native");

    return {
        FilesModal: ({
            onSubmit,
            attachments,
        }: {
            onSubmit: () => void;
            attachments: unknown[];
        }) => (
            <Pressable
                testID="files-modal-submit"
                onPress={onSubmit}
                disabled={attachments.length === 0}
            >
                <Text>modal submit</Text>
            </Pressable>
        ),
        useAttachments: () => ({
            attachments: mockAttachments,
            isFilesModalVisible: false,
            setIsFilesModalVisible: mockSetIsFilesModalVisible,
            addAttachments: mockAddAttachments,
            handleAttachFiles: mockHandleAttachFiles,
            removeAttachment: mockRemoveAttachment,
            clearAttachments: mockClearAttachments,
        }),
    };
});

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
    MentionSuggestions: (props: {
        items: Array<{ userId: string; displayName: string }>;
        onSelect: (item: { userId: string; displayName: string }) => void;
    }) => mockMentionSuggestions(props),
    findMentionAtCursor: () => mockMentionToken,
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
        FileIcon: Icon,
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
        mockAttachments = [];
        mockSetIsFilesModalVisible.mockClear();
        mockAddAttachments.mockClear();
        mockHandleAttachFiles.mockClear();
        mockRemoveAttachment.mockClear();
        mockClearAttachments.mockClear();
        mockConversationParticipants = [];
        mockMentionToken = null;
        mockMentionSuggestions = () => null;
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

    it("passes selected mention ids when sending", async () => {
        const onSend = jest.fn();
        const preventDefault = jest.fn();
        mockConversationParticipants = [
            {
                userId: "peer-1",
                role: "Member",
                displayName: "Mikhail Mikhailets",
                avatarUrl: "",
            },
        ];
        mockMentionToken = { start: 0, end: 3, query: "mik" };
        mockMentionSuggestions = ({ items, onSelect }) => (
            <>
                {items.map((item) => (
                    <Pressable
                        key={item.userId}
                        testID={`mention-suggestion-${item.userId}`}
                        onPress={() => onSelect(item)}
                    >
                        <Text>{item.displayName}</Text>
                    </Pressable>
                ))}
            </>
        );

        render(<ChatInput conversationId="conversation-1" onSend={onSend} />);

        fireEvent.changeText(screen.getByTestId("chat-input-text"), "@mi");
        fireEvent.press(screen.getByTestId("mention-suggestion-peer-1"));

        await act(async () => {
            fireEvent(screen.getByTestId("chat-input-text"), "keyPress", {
                nativeEvent: { key: "Enter", shiftKey: false, preventDefault },
                preventDefault,
            });
            await new Promise((resolve) => requestAnimationFrame(resolve));
        });

        expect(onSend).toHaveBeenCalledWith(
            "@Mikhail Mikhailets",
            [],
            undefined,
            ["peer-1"],
        );
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

    it("submits the current composer text and attachments from the files modal", async () => {
        const attachment: DocumentPickerAsset = {
            name: "lecture.pdf",
            uri: "file://lecture.pdf",
            size: 2048,
            mimeType: "application/pdf",
            lastModified: 1,
        };
        mockAttachments = [attachment];
        const onSend = jest.fn();

        render(<ChatInput conversationId="conversation-1" onSend={onSend} />);
        fireEvent.changeText(screen.getByTestId("chat-input-text"), "materials");

        await act(async () => {
            fireEvent.press(screen.getByTestId("files-modal-submit"));
        });

        expect(onSend).toHaveBeenCalledWith("materials", [attachment], undefined);
        expect(mockClearAttachments).toHaveBeenCalled();
    });
});
