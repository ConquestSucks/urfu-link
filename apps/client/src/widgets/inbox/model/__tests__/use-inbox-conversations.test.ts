import { renderHook } from "@testing-library/react-native";

import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useParticipantsStore } from "@/entities/conversation/model/participants-store";
import {
    toPresenceTypingConversationId,
    usePresenceStore,
} from "@/entities/presence/model/presence-store";
import { useInboxConversations } from "../use-inbox-conversations";

const mockWatchUserPresence = jest.fn(async () => undefined);
const mockUnwatchUserPresence = jest.fn();

jest.mock("@microsoft/signalr", () => ({
    HubConnectionState: {
        Connected: "Connected",
        Disconnected: "Disconnected",
    },
}));

jest.mock("@/shared/lib/signalr", () => ({
    createHubConnection: jest.fn(),
}));

jest.mock("@/shared/store/auth-store", () => {
    const authState = { userId: "user-1" };
    return {
        useCurrentUserId: () => authState.userId,
        useAuthStore: {
            getState: () => authState,
        },
    };
});

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        chat: {
            getConversationParticipants: jest.fn(),
            getConversations: jest.fn(),
            getConversationMessages: jest.fn(),
            editMessage: jest.fn(),
            deleteMessage: jest.fn(),
            forwardMessages: jest.fn(),
            addReaction: jest.fn(),
            removeReaction: jest.fn(),
            pinMessage: jest.fn(),
            unpinMessage: jest.fn(),
        },
    },
}));

describe("useInboxConversations", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        useChatStore.setState({
            connection: null,
            isConnected: false,
            conversations: [],
            isConversationsLoading: false,
            messagesByConversation: {},
            unreadByConversation: {},
            cursors: {},
            hasMoreByConversation: {},
            messagesLoadingByConversation: {},
            messagesLoadedByConversation: {},
            isLoading: false,
            pendingScrollToMessageId: null,
            threadEventListeners: new Set(),
        });
        useParticipantsStore.setState({
            byConversationId: {},
            inflight: {},
        });
        usePresenceStore.setState({
            typingByConversation: {},
            watchUserPresence: mockWatchUserPresence,
            unwatchUserPresence: mockUnwatchUserPresence,
        });
    });

    it("hides empty direct drafts from the inbox list", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-draft",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T10:00:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:00:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current).toEqual([]);
    });

    it("maps the latest local message, date, and unread state into inbox rows", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
            messagesByConversation: {
                "direct-1": [
                    {
                        id: "message-1",
                        conversationId: "direct-1",
                        senderId: "peer-1",
                        body: "latest incoming",
                        attachments: [],
                        state: "Sent",
                        createdAt: "2026-05-24T10:03:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                ],
            },
            unreadByConversation: { "direct-1": 1 },
        });
        useParticipantsStore.setState({
            byConversationId: {
                "direct-1": {
                    items: [
                        {
                            userId: "peer-1",
                            role: "Member",
                            displayName: "Peer User",
                            avatarUrl: "https://example.test/avatar.png",
                        },
                    ],
                    fetchedAt: Date.now(),
                },
            },
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current).toHaveLength(1);
        expect(result.current[0]).toEqual(
            expect.objectContaining({
                id: "direct-1",
                name: "Peer User",
                avatarUrl: "https://example.test/avatar.png",
                message: "latest incoming",
                unreadCount: 1,
                lastMessageFromSelf: false,
                lastMessageRead: false,
            }),
        );
        expect(result.current[0].time).not.toBe("");
        expect(result.current[0].time).not.toBe("Invalid Date");
    });

    it("uses backend unread count before messages are loaded", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:03:00.000Z",
                    lastMessagePreview: {
                        messageId: "message-3",
                        senderId: "peer-1",
                        body: "offline message",
                        sentAtUtc: "2026-05-24T10:03:00.000Z",
                    },
                    unreadCount: 3,
                },
            ],
        });
        useParticipantsStore.setState({
            byConversationId: {
                "direct-1": {
                    items: [
                        {
                            userId: "peer-1",
                            role: "Member",
                            displayName: "Peer User",
                            avatarUrl: "https://example.test/avatar.png",
                        },
                    ],
                    fetchedAt: Date.now(),
                },
            },
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current).toHaveLength(1);
        expect(result.current[0]).toEqual(
            expect.objectContaining({
                id: "direct-1",
                message: "offline message",
                unreadCount: 3,
            }),
        );
    });

    it("uses peer typing as the inbox preview instead of the latest message", () => {
        const conversationId = "d39b2933cccdd8b2812a2b8f401fb2a9d9f6abcd";
        const presenceConversationId = toPresenceTypingConversationId(conversationId);

        useChatStore.setState({
            conversations: [
                {
                    id: conversationId,
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: {
                        senderId: "user-1",
                        body: "old outgoing",
                        sentAtUtc: "2026-05-24T10:03:00.000Z",
                    },
                },
            ],
        });
        usePresenceStore.setState({
            typingByConversation: {
                [presenceConversationId]: [
                    {
                        conversationId: presenceConversationId,
                        userId: "peer-1",
                    },
                ],
            },
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current).toHaveLength(1);
        expect(result.current[0]).toEqual(
            expect.objectContaining({
                id: conversationId,
                message: "Печатает",
                isTyping: true,
            }),
        );
    });

    it("uses preview read state for an own latest message before messages are loaded", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-read",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:03:00.000Z",
                    lastMessagePreview: {
                        messageId: "message-1",
                        senderId: "user-1",
                        body: "already read",
                        sentAtUtc: "2026-05-24T10:03:00.000Z",
                        readAtUtc: "2026-05-24T10:04:00.000Z",
                    },
                },
            ],
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current).toHaveLength(1);
        expect(result.current[0]).toEqual(
            expect.objectContaining({
                id: "direct-read",
                message: "already read",
                lastMessageFromSelf: true,
                lastMessageRead: true,
            }),
        );
    });

    it("uses the attachment file name for a file-only preview", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-file",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:03:00.000Z",
                    lastMessagePreview: {
                        messageId: "message-1",
                        senderId: "user-1",
                        body: "",
                        sentAtUtc: "2026-05-24T10:03:00.000Z",
                        hasAttachments: true,
                        attachmentFileNames: ["report.pdf"],
                    },
                },
            ],
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current).toHaveLength(1);
        expect(result.current[0]).toEqual(
            expect.objectContaining({
                id: "direct-file",
                message: "report.pdf",
                lastMessageFromSelf: true,
            }),
        );
    });

    it("summarizes multiple file-only attachments in the preview", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-files",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
            messagesByConversation: {
                "direct-files": [
                    {
                        id: "message-1",
                        conversationId: "direct-files",
                        senderId: "user-1",
                        body: "",
                        attachments: [
                            {
                                mediaAssetId: "asset-1",
                                type: "Document",
                                fileName: "report.pdf",
                                size: 100,
                                mimeType: "application/pdf",
                            },
                            {
                                mediaAssetId: "asset-2",
                                type: "Document",
                                fileName: "data.json",
                                size: 50,
                                mimeType: "application/json",
                            },
                            {
                                mediaAssetId: "asset-3",
                                type: "Image",
                                fileName: "diagram.png",
                                size: 150,
                                mimeType: "image/png",
                            },
                        ],
                        state: "Sent",
                        createdAt: "2026-05-24T10:03:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                ],
            },
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current).toHaveLength(1);
        expect(result.current[0]).toEqual(
            expect.objectContaining({
                id: "direct-files",
                message: "report.pdf и еще 2 файла",
                lastMessageFromSelf: true,
            }),
        );
    });

    it("does not show the current user's typing as an inbox preview", () => {
        const conversationId = "d39b2933cccdd8b2812a2b8f401fb2a9d9f6abcd";
        const presenceConversationId = toPresenceTypingConversationId(conversationId);

        useChatStore.setState({
            conversations: [
                {
                    id: conversationId,
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: {
                        senderId: "peer-1",
                        body: "real latest",
                        sentAtUtc: "2026-05-24T10:03:00.000Z",
                    },
                },
            ],
        });
        usePresenceStore.setState({
            typingByConversation: {
                [presenceConversationId]: [
                    {
                        conversationId: presenceConversationId,
                        userId: "user-1",
                    },
                ],
            },
        });

        const { result } = renderHook(() => useInboxConversations("chats"));

        expect(result.current[0]).toEqual(
            expect.objectContaining({
                message: "real latest",
                isTyping: false,
            }),
        );
    });

    it("watches direct peers so typing previews arrive while only the inbox is open", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["user-1", "peer-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: {
                        senderId: "peer-1",
                        body: "real latest",
                        sentAtUtc: "2026-05-24T10:03:00.000Z",
                    },
                },
            ],
        });
        useParticipantsStore.setState({
            byConversationId: {
                "direct-1": {
                    items: [
                        {
                            userId: "peer-1",
                            role: "Member",
                            displayName: "Peer User",
                            avatarUrl: "",
                        },
                    ],
                    fetchedAt: Date.now(),
                },
            },
        });

        const { unmount } = renderHook(() => useInboxConversations("chats"));

        expect(mockWatchUserPresence).toHaveBeenCalledWith("peer-1");

        unmount();

        expect(mockUnwatchUserPresence).toHaveBeenCalledWith("peer-1");
    });

    it("maps discipline general and subgroup chats for grouped subject inbox", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "discipline:abc",
                    type: "Group",
                    participants: ["user-1", "student-1"],
                    groupSubtype: "Discipline",
                    disciplineId: "discipline-1",
                    disciplineTitle: "Математика",
                    disciplineChatKind: "General",
                    title: "Математика",
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
                {
                    id: "discipline:abc:subgroup:def",
                    type: "Group",
                    participants: ["user-1", "student-1"],
                    groupSubtype: "Discipline",
                    disciplineId: "discipline-1",
                    disciplineTitle: "Математика",
                    disciplineChatKind: "Subgroup",
                    disciplineSubgroupName: "ПИ-101",
                    title: "ПИ-101",
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        const { result } = renderHook(() => useInboxConversations("subjects"));

        expect(result.current).toEqual([
            expect.objectContaining({
                id: "discipline:abc",
                name: "Общий чат",
                disciplineId: "discipline-1",
                disciplineTitle: "Математика",
                disciplineChatKind: "General",
            }),
            expect.objectContaining({
                id: "discipline:abc:subgroup:def",
                name: "ПИ-101",
                disciplineId: "discipline-1",
                disciplineTitle: "Математика",
                disciplineChatKind: "Subgroup",
            }),
        ]);
    });
});
