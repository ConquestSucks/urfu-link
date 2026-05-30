import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";

import type { CallSessionDto } from "@urfu-link/api-client";

const mockRouter = {
    push: jest.fn(),
};

const mockCalls = {
    cancel: jest.fn(),
};

const mockStartCallRingtone = jest.fn(async (_kind: unknown) => true);
const mockStopCallRingtone = jest.fn(async (_kind: unknown) => undefined);

jest.mock("@/shared/lib/config", () => ({
    appConfig: {
        appEnv: "dev",
        apiUrl: "http://localhost:5080",
        keycloakUrl: "http://localhost:8080",
    },
}));

jest.mock("expo-router", () => ({
    useRouter: () => mockRouter,
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "user-1",
}));

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        calls: mockCalls,
    },
}));

jest.mock("@/shared/ui", () => ({
    Avatar: ({ name }: { name?: string }) => {
        const { Text } = require("react-native");
        return <Text testID="avatar">{name}</Text>;
    },
    ModalOverlay: ({ children, visible }: { children: React.ReactNode; visible?: boolean }) =>
        visible ? <>{children}</> : null,
}));

jest.mock("@/shared/lib/call-sounds", () => ({
    startCallRingtone: (kind: unknown) => mockStartCallRingtone(kind),
    stopCallRingtone: (kind: unknown) => mockStopCallRingtone(kind),
}));

const { OutgoingCallModal } = require("../OutgoingCallModal");
const { useCallStore } = require("@/entities/call/model/call-store");
const { useChatStore } = require("@/entities/conversation/model/chat-store");
const { useParticipantsStore } = require("@/entities/conversation/model/participants-store");

const makeCall = (overrides: Partial<CallSessionDto> = {}): CallSessionDto => ({
    id: "call-1",
    conversationId: "conversation-1",
    callerId: "user-1",
    participantIds: ["user-1", "user-2"],
    callType: "Audio",
    status: "Ringing",
    createdAtUtc: "2026-05-30T10:00:00.000Z",
    ringExpiresAtUtc: "2026-05-30T10:00:45.000Z",
    acceptedAtUtc: null,
    endedAtUtc: null,
    endReason: null,
    participants: [
        { userId: "user-1", isConnected: true },
        { userId: "user-2", isConnected: false },
    ],
    ...overrides,
});

describe("OutgoingCallModal", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        useCallStore.getState().clearCallCache();
        useChatStore.setState({
            conversations: [
                {
                    id: "conversation-1",
                    type: "Direct",
                    participants: ["user-1", "user-2"],
                    createdAtUtc: "2026-05-30T09:59:00.000Z",
                    lastMessageAtUtc: "2026-05-30T09:59:00.000Z",
                    lastMessagePreview: null,
                },
            ],
        });
        useParticipantsStore.getState().prime("conversation-1", [
            {
                userId: "user-1",
                role: "Member",
                displayName: "Алиса",
                avatarUrl: "https://cdn.test/alice.png",
            },
            {
                userId: "user-2",
                role: "Member",
                displayName: "Боб",
                avatarUrl: "https://cdn.test/bob.png",
            },
        ]);
    });

    it("shows outgoing connection state and starts ringtone", async () => {
        useCallStore.getState().setOutgoingCall(makeCall());

        render(<OutgoingCallModal />);

        expect(screen.getByText("Устанавливаем соединение")).toBeTruthy();
        expect(screen.getAllByText("Боб").length).toBeGreaterThan(0);
        await waitFor(() => expect(mockStartCallRingtone).toHaveBeenCalledWith("outgoing"));
    });

    it("cancels outgoing call and stops ringtone", async () => {
        mockCalls.cancel.mockResolvedValue(
            makeCall({ status: "Ended", endReason: "CancelledByCaller" }),
        );
        useCallStore.getState().setOutgoingCall(makeCall());

        render(<OutgoingCallModal />);

        fireEvent.press(screen.getByText("Отменить"));

        await waitFor(() => {
            expect(mockCalls.cancel).toHaveBeenCalledWith("call-1");
            expect(mockStopCallRingtone).toHaveBeenCalledWith("outgoing");
        });
    });

    it("does not open a call screen from a stale outgoing id after cancel", async () => {
        mockCalls.cancel.mockResolvedValue(
            makeCall({ status: "Ended", endReason: "CancelledByCaller" }),
        );
        useCallStore.getState().setOutgoingCall(makeCall());

        render(<OutgoingCallModal />);

        fireEvent.press(screen.getByText("Отменить"));

        await waitFor(() => {
            expect(mockCalls.cancel).toHaveBeenCalledWith("call-1");
        });

        act(() => {
            useCallStore.getState().setActiveCall(makeCall({ status: "Active" }));
        });

        expect(mockRouter.push).not.toHaveBeenCalled();
    });

    it("opens the call screen after accept event", async () => {
        useCallStore.getState().setOutgoingCall(makeCall());
        render(<OutgoingCallModal />);

        act(() => {
            useCallStore.getState().handleCallAccepted(makeCall({ status: "Active" }));
        });

        await waitFor(() => {
            expect(mockStopCallRingtone).toHaveBeenCalledWith("outgoing");
            expect(mockRouter.push).toHaveBeenCalledWith("/call/call-1");
        });
    });
});
