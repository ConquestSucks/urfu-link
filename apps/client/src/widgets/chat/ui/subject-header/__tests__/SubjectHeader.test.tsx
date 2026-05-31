import { render, screen } from "@testing-library/react-native";

import { SubjectHeader } from "../SubjectHeader";

let mockConversations: any[] = [];

jest.mock("@/shared/lib/safeGoBack", () => ({
    safeGoBack: jest.fn(),
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: false }),
}));

jest.mock("expo-router", () => ({
    useRouter: () => ({ push: jest.fn() }),
}));

jest.mock("@/shared/ui", () => ({
    Avatar: () => {
        const { Text } = require("react-native");
        return <Text testID="subject-avatar">avatar</Text>;
    },
}));

jest.mock("@/shared/ui/phosphor", () => ({
    CaretLeftIcon: () => null,
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector: (state: unknown) => unknown) =>
        selector({
            conversations: mockConversations,
            updateConversation: jest.fn(),
        }),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useConversationParticipants: () => [],
    useParticipantsStore: (selector: (state: unknown) => unknown) =>
        selector({
            prime: jest.fn(),
        }),
}));

jest.mock("@/entities/conversation/model/direct-draft-cache", () => ({
    saveDirectDraftConversation: jest.fn(),
}));

jest.mock("@/entities/user", () => ({
    useCurrentUser: () => ({
        data: {
            identity: { name: "Current User" },
            account: { avatarUrl: null },
            notifications: { mutedConversationIds: [] },
        },
    }),
    useMuteConversationNotifications: () => ({ isPending: false, mutate: jest.fn() }),
    useUnmuteConversationNotifications: () => ({ isPending: false, mutate: jest.fn() }),
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "current-user",
}));

jest.mock("@/entities/presence", () => ({
    usePresenceStore: (selector: (state: unknown) => unknown) =>
        selector({
            presenceByUser: {},
            watchUserPresence: jest.fn(),
            unwatchUserPresence: jest.fn(),
            loadBatchPresence: jest.fn().mockResolvedValue(undefined),
        }),
}));

jest.mock("@/entities/call", () => ({
    useCallStore: (selector: (state: unknown) => unknown) =>
        selector({
            startCall: jest.fn(),
        }),
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        chat: {
            openDirectConversation: jest.fn(),
        },
    },
}));

jest.mock("../SubjectHeaderActions", () => ({
    SubjectHeaderActions: () => {
        const { Text } = require("react-native");
        return <Text testID="subject-header-actions">actions</Text>;
    },
}));

jest.mock("../SubjectMembersModal", () => ({
    SubjectMembersModal: () => null,
}));

jest.mock("../../chat-header/UserProfileModal", () => ({
    UserProfileModal: () => null,
}));

describe("SubjectHeader", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockConversations = [];
    });

    it("keeps hook order stable when the subject conversation loads after direct navigation", () => {
        const { rerender } = render(<SubjectHeader subjectId="subject-1" />);

        expect(screen.queryByTestId("subject-header-actions")).toBeNull();

        mockConversations = [
            {
                id: "subject-1",
                type: "Group",
                groupSubtype: "Discipline",
                title: "Algorithms",
                disciplineTitle: "Algorithms",
                disciplineChatKind: "General",
                participantRoles: { "current-user": "Student" },
                capabilities: {},
            },
        ];

        expect(() => rerender(<SubjectHeader subjectId="subject-1" />)).not.toThrow();
        expect(screen.getByTestId("subject-header-actions")).toBeTruthy();
    });
});
