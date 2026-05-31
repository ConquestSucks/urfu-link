import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import type { ReactNode } from "react";

import { ForwardPickerModal } from "../ForwardPickerModal";

const mockForwardMessages = jest.fn();
const mockLoadParticipants = jest.fn();
const mockUpdateConversation = jest.fn();
const mockPrimeParticipants = jest.fn();
const mockSearchUsers = jest.fn();
const mockOpenDirectConversation = jest.fn();

let mockChatState: {
    conversations: unknown[];
    messagesByConversation: Record<string, unknown[]>;
    forwardMessages: jest.Mock;
    updateConversation: jest.Mock;
};

let mockParticipantsState: {
    byConversationId: Record<string, { items: unknown[]; fetchedAt: number }>;
    load: jest.Mock;
    prime: jest.Mock;
};

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: typeof mockChatState) => unknown) =>
        selector(mockChatState),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useParticipantsStore: (selector: (state: typeof mockParticipantsState) => unknown) =>
        selector(mockParticipantsState),
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "user-1",
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        users: {
            searchUsers: (...args: unknown[]) => mockSearchUsers(...args),
        },
        chat: {
            openDirectConversation: (...args: unknown[]) => mockOpenDirectConversation(...args),
        },
    },
}));

jest.mock("@/shared/ui", () => {
    const { Text } = require("react-native");

    return {
        ModalOverlay: ({ children }: { children: ReactNode }) => <>{children}</>,
        Avatar: ({ name, src }: { name?: string; src?: string | null }) => (
            <Text testID={`avatar-${name ?? "empty"}`}>{`${name ?? ""}|${src ?? ""}`}</Text>
        ),
        EmptyState: ({ title }: { title: string }) => <Text>{title}</Text>,
    };
});

jest.mock("@/shared/ui/phosphor", () => ({
    ChatsCircleIcon: () => null,
}));

describe("ForwardPickerModal", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockLoadParticipants.mockResolvedValue([]);
        mockChatState = {
            conversations: [
                {
                    id: "draft-direct",
                    type: "Direct",
                    participants: ["user-1", "peer-draft"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
                {
                    id: "real-direct",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T10:00:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:01:00.000Z",
                    lastMessagePreview: {
                        messageId: "message-1",
                        senderId: "peer-1",
                        body: "hello",
                        sentAtUtc: "2026-05-24T10:01:00.000Z",
                        readAtUtc: null,
                    },
                },
            ],
            messagesByConversation: {},
            forwardMessages: mockForwardMessages,
            updateConversation: mockUpdateConversation,
        };
        mockParticipantsState = {
            byConversationId: {
                "real-direct": {
                    items: [
                        {
                            userId: "peer-1",
                            role: "Member",
                            displayName: "Dev Student",
                            avatarUrl: "https://cdn.test/dev.png",
                        },
                    ],
                    fetchedAt: Date.now(),
                },
            },
            load: mockLoadParticipants,
            prime: mockPrimeParticipants,
        };
        mockSearchUsers.mockResolvedValue({ items: [], hasMore: false });
        mockOpenDirectConversation.mockResolvedValue({
            id: "opened-direct",
            type: "Direct",
            participants: ["user-1", "searched-user"],
            lastMessagePreview: null,
        });
    });

    it("hides direct drafts and shows peer identity for real chats", () => {
        render(<ForwardPickerModal messageIds={["message-1"]} onClose={jest.fn()} />);

        expect(screen.queryByTestId("forward-conversation-draft-direct")).toBeNull();
        expect(screen.getByTestId("forward-conversation-real-direct")).toBeTruthy();
        expect(screen.getByText("Dev Student")).toBeTruthy();
        expect(screen.getByTestId("avatar-Dev Student").props.children).toBe(
            "Dev Student|https://cdn.test/dev.png",
        );
    });

    it("forwards to the selected conversation", async () => {
        const onClose = jest.fn();
        render(<ForwardPickerModal messageIds={["message-1"]} onClose={onClose} />);

        fireEvent.press(screen.getByTestId("forward-conversation-real-direct"));

        await waitFor(() =>
            expect(mockForwardMessages).toHaveBeenCalledWith("real-direct", ["message-1"]),
        );
        expect(onClose).toHaveBeenCalled();
    });

    it("warms participants for visible direct chats without cached identities", () => {
        mockParticipantsState.byConversationId = {};

        render(<ForwardPickerModal messageIds={["message-1"]} onClose={jest.fn()} />);

        expect(mockLoadParticipants).toHaveBeenCalledWith("real-direct");
        expect(mockLoadParticipants).not.toHaveBeenCalledWith("draft-direct");
    });

    it("shows discipline context for subgroup targets", () => {
        mockChatState.conversations = [
            {
                id: "subject-subgroup",
                type: "Group",
                participants: ["user-1", "student-1"],
                title: "Subgroup A",
                disciplineTitle: "Distributed Systems",
                disciplineChatKind: "Subgroup",
                disciplineSubgroupName: "A-01",
                lastMessagePreview: null,
            },
        ];

        render(<ForwardPickerModal messageIds={["message-1"]} onClose={jest.fn()} />);

        expect(screen.getByText("A-01")).toBeTruthy();
        expect(screen.getByText("Distributed Systems · Подгруппа")).toBeTruthy();
    });

    it("opens a direct chat from user search before forwarding", async () => {
        mockSearchUsers.mockResolvedValue({
            items: [
                {
                    id: "searched-user",
                    displayName: "Search Student",
                    username: "search.student",
                    avatarUrl: "https://cdn.test/search.png",
                },
            ],
            hasMore: false,
        });
        const onClose = jest.fn();

        render(<ForwardPickerModal messageIds={["message-1"]} onClose={onClose} />);

        fireEvent.changeText(screen.getByTestId("forward-user-search"), "search");

        await waitFor(() => expect(screen.getByText("Search Student")).toBeTruthy());
        fireEvent.press(screen.getByTestId("forward-user-searched-user"));

        await waitFor(() =>
            expect(mockForwardMessages).toHaveBeenCalledWith(
                "opened-direct",
                ["message-1"],
                "searched-user",
            ),
        );
        expect(mockOpenDirectConversation).toHaveBeenCalledWith("searched-user");
        expect(mockUpdateConversation).toHaveBeenCalledWith(
            expect.objectContaining({ id: "opened-direct" }),
        );
        expect(mockPrimeParticipants).toHaveBeenCalledWith("opened-direct", [
            expect.objectContaining({ userId: "searched-user" }),
        ]);
        expect(onClose).toHaveBeenCalled();
    });
});
