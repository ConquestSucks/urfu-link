import { fireEvent, render, screen } from "@testing-library/react-native";

import { PinnedMessagesModal } from "../PinnedMessagesModal";

const mockLoadPinnedMessages = jest.fn();
const mockUnpinMessage = jest.fn();

let mockState: {
    pinnedMessagesByConversation: Record<string, any[]>;
    pinnedLoadingByConversation: Record<string, boolean>;
    loadPinnedMessages: typeof mockLoadPinnedMessages;
    unpinMessage: typeof mockUnpinMessage;
};

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: typeof mockState) => unknown) => selector(mockState),
}));

jest.mock("@/shared/ui", () => ({
    ModalOverlay: ({ children, visible }: { children: React.ReactNode; visible: boolean }) =>
        visible ? <>{children}</> : null,
}));

jest.mock("@/shared/ui/activity-indicator", () => ({
    ActivityIndicator: () => {
        const { Text } = require("react-native");
        return <Text testID="loading-pinned">loading</Text>;
    },
}));

jest.mock("@/shared/ui/phosphor", () => {
    const makeIcon = (testID: string) => () => {
        const { Text } = require("react-native");
        return <Text testID={testID}>{testID}</Text>;
    };

    return {
        PushPinIcon: makeIcon("pin-icon"),
        XIcon: makeIcon("x-icon"),
    };
});

describe("PinnedMessagesModal", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockState = {
            pinnedMessagesByConversation: {
                "conversation-1": [
                    {
                        id: "message-1",
                        conversationId: "conversation-1",
                        senderId: "user-1",
                        body: "Important pinned text",
                        attachments: [],
                        state: "Sent",
                        createdAt: "2026-05-24T10:00:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                ],
            },
            pinnedLoadingByConversation: {},
            loadPinnedMessages: mockLoadPinnedMessages,
            unpinMessage: mockUnpinMessage,
        };
    });

    it("loads, renders, jumps, and unpins pinned messages", () => {
        const onJumpToMessage = jest.fn();
        render(
            <PinnedMessagesModal
                visible
                conversationId="conversation-1"
                onClose={jest.fn()}
                onJumpToMessage={onJumpToMessage}
            />,
        );

        expect(mockLoadPinnedMessages).toHaveBeenCalledWith("conversation-1");
        fireEvent.press(screen.getByText("Important pinned text"));
        expect(onJumpToMessage).toHaveBeenCalledWith("message-1");

        fireEvent.press(screen.getByTestId("pinned-unpin-message-1"));
        expect(mockUnpinMessage).toHaveBeenCalledWith("conversation-1", "message-1");
    });
});
