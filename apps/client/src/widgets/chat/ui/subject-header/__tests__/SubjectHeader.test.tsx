import { render, screen } from "@testing-library/react-native";

import { SubjectHeader } from "../SubjectHeader";

let mockConversations: any[] = [];
let mockParticipants: any[] = [];

jest.mock("@/shared/lib/safeGoBack", () => ({
    safeGoBack: jest.fn(),
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: false }),
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
    useConversationParticipants: () => mockParticipants,
    useParticipantsStore: (selector: (state: unknown) => unknown) =>
        selector({
            prime: jest.fn(),
        }),
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
    UserProfileModal: ({ isOpen, user }: { isOpen: boolean; user: { name: string } }) => {
        const { Text } = require("react-native");
        return <Text testID="user-profile-modal">{isOpen ? user.name : "closed"}</Text>;
    },
}));

describe("SubjectHeader", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockConversations = [];
        mockParticipants = [];
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

    it("opens a controlled participant profile", () => {
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
        mockParticipants = [
            {
                userId: "teacher-1",
                role: "Teacher",
                displayName: "Teacher One",
                avatarUrl: "https://cdn.test/teacher.png",
            },
        ];

        render(<SubjectHeader subjectId="subject-1" profileUserId="teacher-1" />);

        expect(screen.getByTestId("user-profile-modal").props.children).toBe(
            "Teacher One",
        );
    });
});
