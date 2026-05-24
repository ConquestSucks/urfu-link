import { fireEvent, render, screen, act } from "@testing-library/react-native";

import { MessageActionsMenu } from "../MessageActionsMenu";

const mockDeleteMessage = jest.fn();
const mockPinMessage = jest.fn();
const mockUnpinMessage = jest.fn();
const mockSetReply = jest.fn();
const mockSetEditing = jest.fn();

jest.mock("@/shared/ui", () => ({
    ModalOverlay: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        ArrowBendUpLeftIcon: makeIcon("reply-icon"),
        ArrowBendDoubleUpRightIcon: makeIcon("forward-icon"),
        ChecksIcon: makeIcon("checks-icon"),
        CopyIcon: makeIcon("copy-icon"),
        PencilSimpleIcon: makeIcon("edit-icon"),
        PushPinIcon: makeIcon("pin-icon"),
        PushPinSlashIcon: makeIcon("unpin-icon"),
        SmileyIcon: makeIcon("react-icon"),
        TrashIcon: makeIcon("trash-icon"),
    };
});

jest.mock("@/shared/lib/clipboard", () => ({
    copyTextToClipboard: jest.fn(),
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: () => ({
        deleteMessage: mockDeleteMessage,
        pinMessage: mockPinMessage,
        unpinMessage: mockUnpinMessage,
    }),
}));

jest.mock("../../model/composer-store", () => ({
    useComposerStore: () => ({
        setReply: mockSetReply,
        setEditing: mockSetEditing,
    }),
}));

const message = {
    id: "message-1",
    conversationId: "conversation-1",
    senderId: "user-1",
    body: "hello",
    attachments: [],
    state: "Sent" as const,
    createdAt: "2026-05-24T10:00:00.000Z",
    deliveredAt: null,
    readAt: "2026-05-24T10:02:00.000Z",
};

describe("MessageActionsMenu", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("uses a single tombstone delete action for own messages", async () => {
        const onClose = jest.fn();
        render(
            <MessageActionsMenu
                message={message}
                isOwn
                isPinned={false}
                onClose={onClose}
                onForwardRequest={jest.fn()}
                onReactRequest={jest.fn()}
            />,
        );

        expect(screen.getByText("Удалить")).toBeTruthy();
        expect(screen.queryByText("Удалить у меня")).toBeNull();
        expect(screen.queryByText("Удалить у всех")).toBeNull();

        await act(async () => {
            fireEvent.press(screen.getByText("Удалить"));
        });

        expect(mockDeleteMessage).toHaveBeenCalledWith("message-1", "for-everyone");
        expect(onClose).toHaveBeenCalled();
    });

    it("hides delete for peer messages", () => {
        render(
            <MessageActionsMenu
                message={message}
                isOwn={false}
                isPinned={false}
                onClose={jest.fn()}
                onForwardRequest={jest.fn()}
                onReactRequest={jest.fn()}
            />,
        );

        expect(screen.queryByText("Удалить")).toBeNull();
    });

    it("shows a compact read status row for own messages", () => {
        render(
            <MessageActionsMenu
                message={message}
                isOwn
                isPinned={false}
                readStatusLabel="Прочитано 12:02"
                onClose={jest.fn()}
                onForwardRequest={jest.fn()}
                onReactRequest={jest.fn()}
            />,
        );

        expect(screen.getByText("Прочитано 12:02")).toBeTruthy();
    });

    it("adds hover styling to action rows on web", () => {
        render(
            <MessageActionsMenu
                message={message}
                isOwn
                isPinned={false}
                onClose={jest.fn()}
                onForwardRequest={jest.fn()}
                onReactRequest={jest.fn()}
            />,
        );

        expect(screen.getByTestId("message-action-reply").props.className).toContain(
            "hover:bg-white/5",
        );
        expect(screen.getByTestId("message-action-delete").props.className).toContain(
            "hover:bg-danger-500/10",
        );
    });
});
