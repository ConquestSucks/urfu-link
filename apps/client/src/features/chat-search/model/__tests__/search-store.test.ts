const mockOpenDirectConversation = jest.fn();
const mockUpdateConversation = jest.fn();
const mockPrimeParticipants = jest.fn();
const mockLoadParticipants = jest.fn();
const mockStorage = new Map<string, string>();

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        chat: {
            openDirectConversation: (...args: unknown[]) =>
                mockOpenDirectConversation(...args),
            searchMessages: jest.fn(),
        },
        users: {
            searchUsers: jest.fn(),
        },
    },
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: {
        getState: () => ({
            updateConversation: mockUpdateConversation,
        }),
    },
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useParticipantsStore: {
        getState: () => ({
            prime: mockPrimeParticipants,
            load: mockLoadParticipants,
        }),
    },
}));

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

const { useSearchStore } = require("../search-store") as typeof import("../search-store");

describe("search store direct chat drafts", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockStorage.clear();
        useSearchStore.setState({ pendingUserId: null });
    });

    it("primes participants without loading the draft participants endpoint", async () => {
        mockOpenDirectConversation.mockResolvedValue({
            id: "8b52b1fc6ba59e5479e937392652af924b69a5e7",
            type: "Direct",
            participants: ["user-1", "peer-1"],
            createdAtUtc: "2026-05-24T10:00:00.000Z",
            lastMessageAtUtc: "2026-05-24T10:00:00.000Z",
            lastMessagePreview: null,
        });

        const result = await useSearchStore.getState().openDirectWithUser({
            id: "peer-1",
            displayName: "Peer User",
            username: "peer",
            avatarUrl: null,
        });

        expect(result).toBe("8b52b1fc6ba59e5479e937392652af924b69a5e7");
        expect(mockUpdateConversation).toHaveBeenCalled();
        expect(mockPrimeParticipants).toHaveBeenCalledWith("8b52b1fc6ba59e5479e937392652af924b69a5e7", [
            {
                userId: "peer-1",
                role: "Member",
                displayName: "Peer User",
                avatarUrl: "",
            },
        ]);
        expect(mockLoadParticipants).not.toHaveBeenCalled();
        expect(mockStorage.get("chat.directDrafts.v1")).toContain(
            "8b52b1fc6ba59e5479e937392652af924b69a5e7",
        );
    });
});
