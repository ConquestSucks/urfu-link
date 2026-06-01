import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";

import { ChatView } from "../ChatView";

const mockRouter = { push: jest.fn() };
const mockStartCall = jest.fn();
const mockJoinCall = jest.fn();
const mockEnsureCallMediaAccess: jest.Mock = jest.fn(async () => ({ ok: true }));

jest.mock("expo-router", () => ({
    useRouter: () => mockRouter,
}));

jest.mock("@/shared/lib/useInboxRouting", () => ({
    useInboxRouting: () => ({
        currentTab: "chats",
        params: { id: "conversation-1" },
    }),
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ isMobile: false }),
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "user-1",
}));

jest.mock("@/entities/user", () => ({
    useCurrentUser: () => ({
        data: {
            soundVideo: {
                playbackDeviceId: null,
                recordingDeviceId: null,
                webcamDeviceId: null,
                webcamFacingMode: null,
            },
        },
    }),
}));

jest.mock("@/features/call/lib/call-media-preflight", () => ({
    ensureCallMediaAccess: (args: unknown) => mockEnsureCallMediaAccess(args),
}));

jest.mock("@/entities/call", () => ({
    useCallStore: (selector: (state: unknown) => unknown) =>
        selector({
            startCall: mockStartCall,
            joinCall: mockJoinCall,
        }),
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (selector?: (state: unknown) => unknown) => {
        const state = {
            conversations: [
                {
                    id: "conversation-1",
                    type: "Direct",
                    title: "Direct chat",
                    participantRoles: {},
                    capabilities: {},
                    pinnedMessageIds: [],
                },
            ],
            pendingScrollToMessageId: null,
            setPendingScrollToMessageId: jest.fn(),
        };

        return selector ? selector(state) : state;
    },
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useParticipantsStore: {
        getState: () => ({
            load: jest.fn(async () => undefined),
        }),
    },
}));

jest.mock("@/entities/conversation/model/direct-draft-cache", () => ({
    isDirectDraftId: () => false,
    restoreDirectDraftConversation: () => null,
}));

jest.mock("@/entities/conversation/model/direct-draft-status", () => ({
    isDirectDraftConversation: () => false,
}));

jest.mock("@/entities/presence", () => ({
    toChatThreadViewingContext: (id: string) => `thread:${id}`,
    toChatViewingContext: (id: string) => `chat:${id}`,
    usePresenceStore: (selector: (state: unknown) => unknown) =>
        selector({
            setViewingContexts: jest.fn(),
        }),
}));

jest.mock("@/shared/lib/document-visibility", () => ({
    getDocumentVisibility: () => true,
    subscribeDocumentVisibility: () => () => undefined,
}));

jest.mock("@/features/chat-search", () => ({
    LocalSearchPanel: () => null,
}));

jest.mock("@/features/message-actions", () => ({
    ForwardPickerModal: () => null,
    MessageActionsMenu: () => null,
    ReactionPickerModal: () => null,
}));

jest.mock("@/shared/ui", () => ({
    ModalOverlay: ({ children, visible }: { children: React.ReactNode; visible?: boolean }) =>
        visible ? <>{children}</> : null,
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        chat: {
            getReadReceipts: jest.fn(),
        },
    },
}));

jest.mock("../../lib/use-conversation-send", () => ({
    useConversationSend: () => jest.fn(),
}));

jest.mock("../chat-header/ChatHeader", () => ({
    ChatHeader: ({
        onStartAudioCall,
    }: {
        onStartAudioCall?: () => void;
    }) => {
        const { Pressable, Text } = require("react-native");
        return (
            <Pressable testID="start-audio-call" onPress={onStartAudioCall}>
                <Text>start audio</Text>
            </Pressable>
        );
    },
}));

jest.mock("../subject-header/SubjectHeader", () => ({
    SubjectHeader: () => null,
}));

jest.mock("../MessagesList", () => ({
    MessagesList: ({
        onSystemCallJoin,
    }: {
        onSystemCallJoin?: (callId: string) => void;
    }) => {
        const { Pressable, Text } = require("react-native");
        return (
            <Pressable testID="join-system-call" onPress={() => onSystemCallJoin?.("call-1")}>
                <Text>join call</Text>
            </Pressable>
        );
    },
}));

jest.mock("../chat-input/Input", () => ({
    ChatInput: () => null,
}));

jest.mock("../PinnedBar", () => ({
    PinnedBar: () => null,
}));

jest.mock("../PinnedMessagesModal", () => ({
    PinnedMessagesModal: () => null,
}));

jest.mock("../thread/ThreadPanel", () => ({
    ThreadPanel: () => null,
}));

describe("ChatView call preflight", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        mockStartCall.mockResolvedValue({
            id: "call-1",
            conversationId: "conversation-1",
            callType: "Audio",
            status: "Ringing",
        });
        mockJoinCall.mockResolvedValue({
            id: "call-1",
            conversationId: "conversation-1",
            callType: "Audio",
            status: "Active",
        });
    });

    it("runs media preflight before starting a conversation call", async () => {
        render(<ChatView />);

        fireEvent.press(screen.getByTestId("start-audio-call"));

        await waitFor(() => {
            expect(mockEnsureCallMediaAccess).toHaveBeenCalledWith(
                expect.objectContaining({ callType: "Audio" }),
            );
            expect(mockStartCall).toHaveBeenCalledWith("conversation-1", "Audio");
        });
    });

    it("does not start a call when media preflight fails", async () => {
        mockEnsureCallMediaAccess.mockResolvedValueOnce({
            ok: false,
            errorMessage: "Не удалось получить доступ к микрофону. Проверьте разрешения браузера.",
        });
        render(<ChatView />);

        fireEvent.press(screen.getByTestId("start-audio-call"));

        await waitFor(() => {
            expect(screen.getByText("Не удалось получить доступ к микрофону. Проверьте разрешения браузера.")).toBeTruthy();
        });
        expect(mockStartCall).not.toHaveBeenCalled();
    });

    it("runs media preflight before joining a system call", async () => {
        render(<ChatView />);

        fireEvent.press(screen.getByTestId("join-system-call"));

        await waitFor(() => {
            expect(mockEnsureCallMediaAccess).toHaveBeenCalledWith(
                expect.objectContaining({ callType: "Audio" }),
            );
            expect(mockJoinCall).toHaveBeenCalledWith("call-1");
            expect(mockRouter.push).toHaveBeenCalledWith("/call/call-1");
        });
    });
});
