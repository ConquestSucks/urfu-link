import { mapMessageToProps, useChatStore } from "../chat-store";
import { createHubConnection } from "@/shared/lib/signalr";
import { apiClient } from "@/shared/lib/api";
import { playMessageSound } from "@/shared/lib/message-sounds";
import { showMessageBrowserNotification } from "@/shared/lib/browser-notifications";

const mockAuthState: { userId: string | null; setUserId: jest.Mock } = {
    userId: "user-1",
    setUserId: jest.fn((userId: string | null) => {
        mockAuthState.userId = userId;
    }),
};

jest.mock("@microsoft/signalr", () => ({
    HubConnectionState: {
        Connected: "Connected",
        Disconnected: "Disconnected",
    },
}));

const hubHandlers: Record<string, (...args: unknown[]) => void> = {};
const hubConnection = {
    state: "Disconnected",
    on: jest.fn((event: string, handler: (...args: unknown[]) => void) => {
        hubHandlers[event] = handler;
    }),
    onclose: jest.fn(),
    onreconnected: jest.fn(),
    onreconnecting: jest.fn(),
    start: jest.fn(async () => {
        hubConnection.state = "Connected";
    }),
    stop: jest.fn(async () => {
        hubConnection.state = "Disconnected";
    }),
    invoke: jest.fn(),
};

jest.mock("@/shared/lib/signalr", () => ({
    createHubConnection: jest.fn(),
}));

jest.mock("@/shared/lib/message-sounds", () => ({
    playMessageSound: jest.fn(),
}));

jest.mock("@/shared/lib/browser-notifications", () => ({
    showMessageBrowserNotification: jest.fn(),
}));

jest.mock("@/shared/store/auth-store", () => ({
    useAuthStore: {
        getState: jest.fn(() => mockAuthState),
    },
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        users: {
            getMe: jest.fn(),
        },
        chat: {
            editMessage: jest.fn(),
            deleteMessage: jest.fn(),
            forwardMessages: jest.fn(),
            addReaction: jest.fn(),
            removeReaction: jest.fn(),
            pinMessage: jest.fn(),
            unpinMessage: jest.fn(),
            getPinnedMessages: jest.fn(),
            getConversations: jest.fn(),
            getConversationMessages: jest.fn(),
        },
    },
}));

describe("chat store direct draft sending", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockAuthState.userId = "user-1";
        Object.keys(hubHandlers).forEach((key) => delete hubHandlers[key]);
        hubConnection.state = "Disconnected";
        (createHubConnection as jest.Mock).mockReturnValue(hubConnection);
        (apiClient.users.getMe as jest.Mock).mockResolvedValue({
            userId: "user-1",
            identity: {
                name: "Current User",
                email: "current@example.com",
                username: "current",
            },
            account: {
                avatarUrl: null,
                aboutMe: null,
            },
            privacy: {
                showOnlineStatus: true,
                showLastVisitTime: true,
            },
            notifications: {
                newMessages: true,
                notificationSound: true,
                disciplineChatMessages: true,
                mentions: true,
                mutedConversationIds: [],
            },
            soundVideo: {
                playbackDeviceId: null,
                recordingDeviceId: null,
                webcamDeviceId: null,
            },
        });
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
            pinnedMessagesByConversation: {},
            pinnedLoadingByConversation: {},
            isLoading: false,
            pendingScrollToMessageId: null,
            threadEventListeners: new Set(),
        });
    });

    it("keeps a direct draft marked as draft while the first message is pending", async () => {
        let resolveInvoke!: (value: unknown) => void;
        let pendingClientMessageId = "";
        const invoke = jest.fn(
            (_method: string, payload: { clientMessageId: string }) => {
                pendingClientMessageId = payload.clientMessageId;
                return new Promise((resolve) => {
                    resolveInvoke = resolve;
                });
            },
        );

        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        const sendPromise = useChatStore.getState().sendMessage("direct-1", "hello");
        await Promise.resolve();
        await Promise.resolve();

        const pendingConversation = useChatStore.getState().conversations[0];
        expect(pendingConversation.lastMessagePreview).toEqual(
            expect.objectContaining({ body: "hello" }),
        );
        expect((pendingConversation as { _localStatus?: string })._localStatus).toBe("draft");

        expect(resolveInvoke).toBeDefined();
        resolveInvoke({
            id: "message-1",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "hello",
            attachments: [],
            state: "Sent",
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            deliveredAtUtc: null,
            readAtUtc: null,
            clientMessageId: pendingClientMessageId,
            editedAtUtc: null,
            replyTo: null,
            reactionsSummary: {},
            mentions: [],
            forwardedFrom: null,
        });
        await sendPromise;

        expect(
            (useChatStore.getState().conversations[0] as { _localStatus?: string })._localStatus,
        ).toBeUndefined();
    });

    it("passes peerUserId when sending the first message to a direct draft", async () => {
        const invoke = jest.fn(async (_method: string, payload: { clientMessageId: string }) => ({
            id: "message-1",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "hello",
            attachments: [],
            state: "Sent",
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            deliveredAtUtc: null,
            readAtUtc: null,
            clientMessageId: payload.clientMessageId,
            editedAtUtc: null,
            replyTo: null,
            reactionsSummary: {},
            mentions: [],
            forwardedFrom: null,
        }));

        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        await useChatStore.getState().sendMessage("direct-1", "hello");

        expect(invoke).toHaveBeenCalledWith(
            "SendMessage",
            expect.objectContaining({
                conversationId: "direct-1",
                body: "hello",
                peerUserId: "peer-1",
            }),
        );

        const stored = useChatStore.getState().messagesByConversation["direct-1"][0];
        expect(stored.id).toBe("message-1");
        expect(stored.createdAt).toBe("2026-05-24T10:00:00.000Z");
        const view = mapMessageToProps(stored, "user-1");
        expect(view.time).not.toBe("Invalid Date");
        expect(view.seen).toBe(false);

        const conversation = useChatStore.getState().conversations[0];
        expect(conversation.lastMessagePreview).toEqual(
            expect.objectContaining({
                senderId: "user-1",
                body: "hello",
                sentAtUtc: "2026-05-24T10:00:00.000Z",
            }),
        );
        expect(conversation.lastMessageAtUtc).toBe("2026-05-24T10:00:00.000Z");
    });

    it("passes selected mention user ids to the hub payload", async () => {
        const invoke = jest.fn(async (_method: string, payload: { clientMessageId: string; mentionUserIds: string[] }) => ({
            id: "message-1",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "@Mikhail Mikhailets привет",
            attachments: [],
            state: "Sent",
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            deliveredAtUtc: null,
            readAtUtc: null,
            clientMessageId: payload.clientMessageId,
            editedAtUtc: null,
            replyTo: null,
            reactionsSummary: {},
            mentions: payload.mentionUserIds,
            forwardedFrom: null,
        }));

        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        await useChatStore.getState().sendMessage(
            "direct-1",
            "@Mikhail Mikhailets привет",
            [],
            undefined,
            ["peer-1"],
        );

        expect(invoke).toHaveBeenCalledWith(
            "SendMessage",
            expect.objectContaining({
                mentionUserIds: ["peer-1"],
            }),
        );
    });

    it("hydrates the current user before sending when the auth store has no jwt user", async () => {
        mockAuthState.userId = null;
        const invoke = jest.fn(async (_method: string, payload: { clientMessageId: string }) => ({
            id: "message-1",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "hello",
            attachments: [],
            state: "Sent",
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            deliveredAtUtc: null,
            readAtUtc: null,
            clientMessageId: payload.clientMessageId,
            editedAtUtc: null,
            replyTo: null,
            reactionsSummary: {},
            mentions: [],
            forwardedFrom: null,
        }));

        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        await useChatStore.getState().sendMessage("direct-1", "hello");

        expect(apiClient.users.getMe).toHaveBeenCalledTimes(1);
        expect(mockAuthState.setUserId).toHaveBeenCalledWith("user-1");
        expect(invoke).toHaveBeenCalledWith(
            "SendMessage",
            expect.objectContaining({
                conversationId: "direct-1",
                peerUserId: "peer-1",
            }),
        );
    });

    it("plays the send sound after a successful message send", async () => {
        const invoke = jest.fn(async (_method: string, payload: { clientMessageId: string }) => ({
            id: "message-1",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "hello",
            attachments: [],
            state: "Sent",
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            deliveredAtUtc: null,
            readAtUtc: null,
            clientMessageId: payload.clientMessageId,
            editedAtUtc: null,
            replyTo: null,
            reactionsSummary: {},
            mentions: [],
            forwardedFrom: null,
        }));

        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        await useChatStore.getState().sendMessage("direct-1", "hello");

        expect(playMessageSound).toHaveBeenCalledWith("send");
    });

    it("does not play the send sound when sending fails", async () => {
        const consoleErrorSpy = jest.spyOn(console, "error").mockImplementation(() => {});
        const invoke = jest.fn(async () => {
            throw new Error("send failed");
        });

        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        try {
            await expect(useChatStore.getState().sendMessage("direct-1", "hello")).rejects.toThrow(
                "send failed",
            );

            expect(playMessageSound).not.toHaveBeenCalled();
            const stored = useChatStore.getState().messagesByConversation["direct-1"][0] as {
                _localStatus?: string;
            };
            expect(stored._localStatus).toBe("failed");
        } finally {
            consoleErrorSpy.mockRestore();
        }
    });

    it("plays the receive sound for a new realtime incoming message only once", async () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        await useChatStore.getState().connect();

        const incoming = {
            id: "message-2",
            conversationId: "direct-1",
            senderId: "peer-1",
            body: "new incoming",
            attachments: [],
            state: "Sent",
            createdAt: "2026-05-24T10:03:00.000Z",
            deliveredAt: null,
            readAt: null,
        };

        hubHandlers.MessageReceived(incoming);
        hubHandlers.MessageReceived(incoming);

        expect(playMessageSound).toHaveBeenCalledTimes(1);
        expect(playMessageSound).toHaveBeenCalledWith("receive", {
            conversationId: "direct-1",
            isDiscipline: false,
        });
        expect(showMessageBrowserNotification).toHaveBeenCalledTimes(1);
        expect(showMessageBrowserNotification).toHaveBeenCalledWith(
            expect.objectContaining({ id: "message-2" }),
            expect.objectContaining({
                currentUserId: "user-1",
                isDiscipline: false,
            }),
        );
    });

    it("does not play the receive sound for own realtime messages", async () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        await useChatStore.getState().connect();

        hubHandlers.MessageReceived({
            id: "message-own",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "own echo",
            attachments: [],
            state: "Sent",
            createdAt: "2026-05-24T10:03:00.000Z",
            deliveredAt: null,
            readAt: null,
        });

        expect(playMessageSound).not.toHaveBeenCalled();
        expect(showMessageBrowserNotification).not.toHaveBeenCalled();
    });

    it("keeps media asset ids in mapped message attachments", () => {
        const view = mapMessageToProps(
            {
                id: "message-1",
                conversationId: "direct-1",
                senderId: "user-1",
                body: "",
                attachments: [
                    {
                        mediaAssetId: "asset-1",
                        type: "Document",
                        fileName: "file.json",
                        size: 128,
                        mimeType: "application/json",
                    },
                ],
                state: "Sent",
                createdAt: "2026-05-24T10:00:00.000Z",
                deliveredAt: null,
                readAt: null,
            },
            "user-1",
        );

        expect(view.attachments).toEqual([
            {
                name: "file.json",
                url: "/api/media/asset-1/download-url",
                mediaAssetId: "asset-1",
                type: "Document",
                mimeType: "application/json",
                durationSeconds: undefined,
            },
        ]);
    });

    it("updates the inbox preview and unread count for an incoming message", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        useChatStore.getState().addMessage({
            id: "message-2",
            conversationId: "direct-1",
            senderId: "peer-1",
            body: "new incoming",
            attachments: [],
            state: "Sent",
            createdAt: "2026-05-24T10:03:00.000Z",
            deliveredAt: null,
            readAt: null,
        });

        const state = useChatStore.getState();
        expect(state.conversations[0].lastMessagePreview).toEqual(
            expect.objectContaining({
                senderId: "peer-1",
                body: "new incoming",
                sentAtUtc: "2026-05-24T10:03:00.000Z",
            }),
        );
        expect(state.conversations[0].lastMessageAtUtc).toBe("2026-05-24T10:03:00.000Z");
        expect(state.unreadByConversation["direct-1"]).toBe(1);
    });

    it("hydrates unread counts returned with the conversations list", async () => {
        (apiClient.chat.getConversations as jest.Mock).mockResolvedValue({
            items: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
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
                {
                    id: "direct-read",
                    type: "Direct",
                    participants: ["peer-2", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:02:00.000Z",
                    lastMessagePreview: {
                        messageId: "message-read",
                        senderId: "peer-2",
                        body: "read message",
                        sentAtUtc: "2026-05-24T10:02:00.000Z",
                    },
                    unreadCount: 0,
                },
            ],
            nextCursor: null,
        });
        useChatStore.setState({
            unreadByConversation: {
                "direct-read": 1,
            },
        });

        await useChatStore.getState().loadConversations("Direct");

        const state = useChatStore.getState();
        expect(state.conversations).toHaveLength(2);
        expect(state.unreadByConversation["direct-1"]).toBe(3);
        expect(state.unreadByConversation["direct-read"]).toBeUndefined();
    });

    it("clears local unread state when marking incoming messages as read", async () => {
        const invoke = jest.fn(async () => "message-2");
        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            unreadByConversation: { "direct-1": 2 },
            messagesByConversation: {
                "direct-1": [
                    {
                        id: "message-2",
                        conversationId: "direct-1",
                        senderId: "peer-1",
                        body: "new incoming",
                        attachments: [],
                        state: "Sent",
                        createdAt: "2026-05-24T10:03:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                    {
                        id: "message-1",
                        conversationId: "direct-1",
                        senderId: "peer-1",
                        body: "older incoming",
                        attachments: [],
                        state: "Sent",
                        createdAt: "2026-05-24T10:02:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                ],
            },
        });

        await useChatStore.getState().markRead("direct-1", "message-2");

        expect(invoke).toHaveBeenCalledWith("MarkRead", "direct-1", "message-2");
        expect(useChatStore.getState().unreadByConversation["direct-1"]).toBeUndefined();
        expect(
            useChatStore
                .getState()
                .messagesByConversation["direct-1"]
                .every((message) => message.readAt !== null),
        ).toBe(true);
    });

    it("does not mark own messages as read when the current user is the reader", async () => {
        await useChatStore.getState().connect();
        useChatStore.setState({
            messagesByConversation: {
                "direct-1": [
                    {
                        id: "message-2",
                        conversationId: "direct-1",
                        senderId: "peer-1",
                        body: "incoming",
                        attachments: [],
                        state: "Sent",
                        createdAt: "2026-05-24T10:01:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                    {
                        id: "message-1",
                        conversationId: "direct-1",
                        senderId: "user-1",
                        body: "outgoing",
                        attachments: [],
                        state: "Sent",
                        createdAt: "2026-05-24T10:00:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                ],
            },
        });

        hubHandlers.MessageReadUpdate("direct-1", "message-2", "user-1");

        const own = useChatStore.getState().messagesByConversation["direct-1"][1];
        expect(mapMessageToProps(own, "user-1").seen).toBe(false);
    });

    it("marks own messages as read when the peer is the reader", async () => {
        await useChatStore.getState().connect();
        useChatStore.setState({
            messagesByConversation: {
                "direct-1": [
                    {
                        id: "message-1",
                        conversationId: "direct-1",
                        senderId: "user-1",
                        body: "outgoing",
                        attachments: [],
                        state: "Sent",
                        createdAt: "2026-05-24T10:00:00.000Z",
                        deliveredAt: null,
                        readAt: null,
                    },
                ],
            },
        });

        hubHandlers.MessageReadByUpdate(
            "direct-1",
            "message-1",
            "peer-1",
            "2026-05-24T10:02:00.000Z",
        );

        const own = useChatStore.getState().messagesByConversation["direct-1"][0];
        expect(mapMessageToProps(own, "user-1").seen).toBe(true);
    });

    it("marks the last conversation preview as read when the message is not loaded", async () => {
        await useChatStore.getState().connect();
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:00:00.000Z",
                    lastMessagePreview: {
                        messageId: "message-1",
                        senderId: "user-1",
                        body: "outgoing",
                        sentAtUtc: "2026-05-24T10:00:00.000Z",
                        readAtUtc: null,
                    },
                },
            ],
            messagesByConversation: {},
        });

        hubHandlers.MessageReadByUpdate(
            "direct-1",
            "message-1",
            "peer-1",
            "2026-05-24T10:02:00.000Z",
        );

        expect(useChatStore.getState().conversations[0].lastMessagePreview).toEqual(
            expect.objectContaining({
                messageId: "message-1",
                readAtUtc: "2026-05-24T10:02:00.000Z",
            }),
        );
    });

    it("loads full pinned message snapshots into the pinned cache", async () => {
        const pinned = [
            {
                id: "message-1",
                conversationId: "direct-1",
                senderId: "user-1",
                body: "pinned",
                attachments: [],
                state: "Sent" as const,
                createdAt: "2026-05-24T10:00:00.000Z",
                deliveredAt: null,
                readAt: null,
            },
        ];
        (apiClient.chat.getPinnedMessages as jest.Mock).mockResolvedValue(pinned);

        await useChatStore.getState().loadPinnedMessages("direct-1");

        expect(apiClient.chat.getPinnedMessages).toHaveBeenCalledWith("direct-1");
        expect(useChatStore.getState().pinnedMessagesByConversation["direct-1"]).toEqual([
            expect.objectContaining({ id: "message-1", body: "pinned" }),
        ]);
        expect(useChatStore.getState().pinnedLoadingByConversation["direct-1"]).toBe(false);
    });

    it("keeps pinned ids and full pinned cache in sync after a PinsUpdated event", () => {
        const pinned = [
            {
                id: "message-1",
                conversationId: "direct-1",
                senderId: "user-1",
                body: "pinned",
                attachments: [],
                state: "Sent" as const,
                createdAt: "2026-05-24T10:00:00.000Z",
                deliveredAt: null,
                readAt: null,
            },
        ];
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        useChatStore.getState().applyPinsUpdated("direct-1", pinned);

        expect(useChatStore.getState().conversations[0].pinnedMessageIds).toEqual(["message-1"]);
        expect(useChatStore.getState().pinnedMessagesByConversation["direct-1"]).toEqual([
            expect.objectContaining({ id: "message-1", body: "pinned" }),
        ]);
    });

    it("applies pinned snapshots returned by hub pin and unpin calls", async () => {
        const pinned = [
            {
                id: "message-1",
                conversationId: "direct-1",
                senderId: "user-1",
                body: "pinned",
                attachments: [],
                state: "Sent" as const,
                createdAt: "2026-05-24T10:00:00.000Z",
                deliveredAt: null,
                readAt: null,
            },
        ];
        const invoke = jest
            .fn()
            .mockResolvedValueOnce(pinned)
            .mockResolvedValueOnce([]);
        useChatStore.setState({
            connection: { state: "Connected", invoke } as never,
            isConnected: true,
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });

        await useChatStore.getState().pinMessage("direct-1", "message-1");
        await useChatStore.getState().unpinMessage("direct-1", "message-1");

        expect(invoke).toHaveBeenNthCalledWith(1, "PinMessage", "direct-1", "message-1");
        expect(invoke).toHaveBeenNthCalledWith(2, "UnpinMessage", "direct-1", "message-1");
        expect(useChatStore.getState().conversations[0].pinnedMessageIds).toEqual([]);
        expect(useChatStore.getState().pinnedMessagesByConversation["direct-1"]).toEqual([]);
    });

    it("updates pinned snapshots when an edited message arrives", () => {
        const original = {
            id: "message-1",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "before edit",
            attachments: [],
            state: "Sent" as const,
            createdAt: "2026-05-24T10:00:00.000Z",
            deliveredAt: null,
            readAt: null,
        };
        useChatStore.setState({
            messagesByConversation: {
                "direct-1": [original],
            },
            pinnedMessagesByConversation: {
                "direct-1": [original],
            },
        });

        useChatStore.getState().applyMessageEdited({
            ...original,
            body: "after edit",
            editedAtUtc: "2026-05-24T10:05:00.000Z",
        });

        expect(useChatStore.getState().messagesByConversation["direct-1"][0]).toEqual(
            expect.objectContaining({
                body: "after edit",
                editedAtUtc: "2026-05-24T10:05:00.000Z",
            }),
        );
        expect(useChatStore.getState().pinnedMessagesByConversation["direct-1"][0]).toEqual(
            expect.objectContaining({
                body: "after edit",
                editedAtUtc: "2026-05-24T10:05:00.000Z",
            }),
        );
    });

    it("updates the last conversation preview when the preview message is edited", () => {
        useChatStore.setState({
            conversations: [
                {
                    id: "direct-1",
                    type: "Direct",
                    participants: ["peer-1", "user-1"],
                    createdAtUtc: "2026-05-24T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-24T10:00:00.000Z",
                    lastMessagePreview: {
                        messageId: "message-1",
                        senderId: "user-1",
                        body: "before edit",
                        sentAtUtc: "2026-05-24T10:00:00.000Z",
                        hasAttachments: false,
                        attachmentFileNames: [],
                        readAtUtc: null,
                    },
                },
            ],
        });

        useChatStore.getState().applyMessageEdited({
            id: "message-1",
            conversationId: "direct-1",
            senderId: "user-1",
            body: "after edit",
            attachments: [
                {
                    mediaAssetId: "asset-1",
                    type: "Document",
                    fileName: "edited.txt",
                    size: 12,
                    mimeType: "text/plain",
                },
            ],
            state: "Sent",
            createdAt: "2026-05-24T10:00:00.000Z",
            deliveredAt: null,
            readAt: "2026-05-24T10:02:00.000Z",
            editedAtUtc: "2026-05-24T10:05:00.000Z",
        });

        expect(useChatStore.getState().conversations[0].lastMessagePreview).toEqual(
            expect.objectContaining({
                messageId: "message-1",
                body: "after edit",
                hasAttachments: true,
                attachmentFileNames: ["edited.txt"],
                readAtUtc: "2026-05-24T10:02:00.000Z",
            }),
        );
    });
});
