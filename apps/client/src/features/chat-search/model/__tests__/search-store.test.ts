const mockOpenDirectConversation = jest.fn();
const mockUpdateConversation = jest.fn();
const mockPrimeParticipants = jest.fn();
const mockLoadParticipants = jest.fn();

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

const { useSearchStore } = require("../search-store") as typeof import("../search-store");

describe("search store direct chat drafts", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        useSearchStore.setState({ pendingUserId: null });
    });

    it("primes participants without loading the draft participants endpoint", async () => {
        mockOpenDirectConversation.mockResolvedValue({
            id: "direct-1",
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

        expect(result).toBe("direct-1");
        expect(mockUpdateConversation).toHaveBeenCalled();
        expect(mockPrimeParticipants).toHaveBeenCalledWith("direct-1", [
            {
                userId: "peer-1",
                role: "Member",
                displayName: "Peer User",
                avatarUrl: "",
            },
        ]);
        expect(mockLoadParticipants).not.toHaveBeenCalled();
    });
});
