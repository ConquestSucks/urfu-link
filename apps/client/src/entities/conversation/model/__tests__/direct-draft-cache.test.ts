const mockStorage = new Map<string, string>();

jest.mock("@/shared/lib/storage", () => ({
    appStorage: {
        getItem: jest.fn((name: string) => mockStorage.get(name) ?? null),
        setItem: jest.fn((name: string, value: string) => {
            mockStorage.set(name, value);
        }),
        removeItem: jest.fn((name: string) => {
            mockStorage.delete(name);
        }),
    },
}));

const {
    isDirectDraftId,
    restoreDirectDraftConversation,
    saveDirectDraftConversation,
} = require("../direct-draft-cache") as typeof import("../direct-draft-cache");

describe("direct draft cache", () => {
    beforeEach(() => {
        mockStorage.clear();
    });

    it("recognizes deterministic direct draft ids", () => {
        expect(isDirectDraftId("8b52b1fc6ba59e5479e937392652af924b69a5e7")).toBe(true);
        expect(isDirectDraftId("8b52b1fc6ba59e5479e93739")).toBe(false);
        expect(isDirectDraftId("not-a-draft")).toBe(false);
    });

    it("stores and restores an unmaterialized direct conversation", () => {
        const conversation = {
            id: "8b52b1fc6ba59e5479e937392652af924b69a5e7",
            type: "Direct" as const,
            participants: ["current-user", "peer-user"],
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            lastMessageAtUtc: "2026-05-24T10:00:00.000Z",
            lastMessagePreview: null,
        };
        const participants = [
            {
                userId: "peer-user",
                role: "Member" as const,
                displayName: "Peer User",
                avatarUrl: "",
            },
        ];

        saveDirectDraftConversation(conversation, participants);

        expect(restoreDirectDraftConversation(conversation.id)).toEqual({
            conversation,
            participants,
        });
    });

    it("does not cache already materialized direct conversations", () => {
        const conversation = {
            id: "8b52b1fc6ba59e5479e937392652af924b69a5e7",
            type: "Direct" as const,
            participants: ["current-user", "peer-user"],
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            lastMessageAtUtc: "2026-05-24T10:01:00.000Z",
            lastMessagePreview: {
                messageId: "message-1",
                senderId: "current-user",
                body: "hello",
            },
        };

        saveDirectDraftConversation(conversation, []);

        expect(restoreDirectDraftConversation(conversation.id)).toBeNull();
    });
});
