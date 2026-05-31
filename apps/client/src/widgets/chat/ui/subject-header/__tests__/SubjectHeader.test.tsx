import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";

import { SubjectHeader } from "../SubjectHeader";

let mockConversations: any[] = [];
let mockParticipants: any[] = [];
const mockUpdateConversation = jest.fn();
const mockPrimeParticipants = jest.fn();
const mockStartCall = jest.fn();
const mockOpenDirectConversation = jest.fn();

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
            updateConversation: mockUpdateConversation,
        }),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useConversationParticipants: () => mockParticipants,
    useParticipantsStore: (selector: (state: unknown) => unknown) =>
        selector({
            prime: mockPrimeParticipants,
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
            startCall: mockStartCall,
        }),
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        chat: {
            openDirectConversation: (...args: unknown[]) =>
                mockOpenDirectConversation(...args),
        },
    },
}));

jest.mock("../SubjectHeaderActions", () => ({
    SubjectHeaderActions: ({ onOpenMembers }: { onOpenMembers: () => void }) => {
        const { Pressable, Text } = require("react-native");
        return (
            <Pressable testID="subject-header-actions" onPress={onOpenMembers}>
                <Text>actions</Text>
            </Pressable>
        );
    },
}));

jest.mock("../SubjectMembersModal", () => ({
    SubjectMembersModal: ({
        isOpen,
        onOpenProfile,
    }: {
        isOpen: boolean;
        onOpenProfile: (userId: string) => void;
    }) => {
        const { Pressable, Text, View } = require("react-native");
        return isOpen ? (
            <View testID="members-modal-open">
                <Pressable testID="open-peer-profile" onPress={() => onOpenProfile("peer-user")}>
                    <Text>peer</Text>
                </Pressable>
            </View>
        ) : null;
    },
}));

jest.mock("../../chat-header/UserProfileModal", () => ({
    UserProfileModal: ({
        isOpen,
        onClose,
        onStartAudioCall,
    }: {
        isOpen: boolean;
        onClose: () => void;
        onStartAudioCall?: () => void;
    }) => {
        const { Pressable, Text, View } = require("react-native");
        return isOpen ? (
            <View testID="profile-modal-open">
                <Pressable
                    testID="start-profile-audio"
                    onPress={() => {
                        onClose();
                        onStartAudioCall?.();
                    }}
                >
                    <Text>audio</Text>
                </Pressable>
            </View>
        ) : null;
    },
}));

describe("SubjectHeader", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockConversations = [];
        mockParticipants = [];
        mockOpenDirectConversation.mockResolvedValue({
            id: "direct-1",
            type: "Direct",
            title: "Peer",
            participantRoles: {},
            capabilities: {},
        });
        mockStartCall.mockResolvedValue({
            id: "call-1",
            conversationId: "direct-1",
            callType: "Audio",
            status: "Ringing",
        });
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

    it("closes member and profile modals before starting a direct call from a profile", async () => {
        const onBeforeStartCall = jest.fn();
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
                userId: "peer-user",
                role: "Student",
                displayName: "Peer User",
                avatarUrl: "peer-avatar",
            },
        ];

        render(<SubjectHeader subjectId="subject-1" onBeforeStartCall={onBeforeStartCall} />);

        fireEvent.press(screen.getByTestId("subject-header-actions"));
        fireEvent.press(screen.getByTestId("open-peer-profile"));
        expect(screen.getByTestId("members-modal-open")).toBeTruthy();
        expect(screen.getByTestId("profile-modal-open")).toBeTruthy();

        fireEvent.press(screen.getByTestId("start-profile-audio"));

        await waitFor(() => {
            expect(mockOpenDirectConversation).toHaveBeenCalledWith("peer-user");
            expect(mockStartCall).toHaveBeenCalledWith("direct-1", "Audio");
        });
        expect(onBeforeStartCall).toHaveBeenCalledTimes(1);
        expect(screen.queryByTestId("members-modal-open")).toBeNull();
        expect(screen.queryByTestId("profile-modal-open")).toBeNull();
    });
});
