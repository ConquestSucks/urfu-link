import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";

import type { CallSessionDto } from "@urfu-link/api-client";

const mockRouter = {
    push: jest.fn(),
};

const mockCalls = {
    accept: jest.fn(),
    decline: jest.fn(),
};

const mockModalOverlay = jest.fn();
const mockStartCallRingtone = jest.fn(async (_kind: unknown) => true);
const mockStopCallRingtone = jest.fn(async (_kind: unknown) => undefined);
const mockEnsureCallMediaAccess: jest.Mock = jest.fn(async () => ({ ok: true }));

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
    ModalOverlay: (props: {
        children: React.ReactNode;
        visible?: boolean;
        backdropClassName?: string;
        contentClassName?: string;
    }) => {
        mockModalOverlay(props);
        return props.visible ? <>{props.children}</> : null;
    },
}));

jest.mock("@/shared/lib/call-sounds", () => ({
    startCallRingtone: (kind: unknown) => mockStartCallRingtone(kind),
    stopCallRingtone: (kind: unknown) => mockStopCallRingtone(kind),
}));

jest.mock("@/features/call/lib/call-media-preflight", () => ({
    ensureCallMediaAccess: (args: unknown) => mockEnsureCallMediaAccess(args),
}));

const { IncomingCallModal } = require("../IncomingCallModal");
const { useCallStore } = require("@/entities/call/model/call-store");
const { useChatStore } = require("@/entities/conversation/model/chat-store");
const { useParticipantsStore } = require("@/entities/conversation/model/participants-store");

const makeCall = (overrides: Partial<CallSessionDto> = {}): CallSessionDto => ({
    id: "call-1",
    conversationId: "conversation-1",
    callerId: "user-2",
    participantIds: ["user-1", "user-2"],
    callType: "Audio",
    status: "Ringing",
    createdAtUtc: "2026-05-30T10:00:00.000Z",
    ringExpiresAtUtc: "2026-05-30T10:00:45.000Z",
    acceptedAtUtc: null,
    endedAtUtc: null,
    endReason: null,
    participants: [
        { userId: "user-1", isConnected: false },
        { userId: "user-2", isConnected: true },
    ],
    ...overrides,
});

describe("IncomingCallModal", () => {
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

    it("renders incoming ringing state inside the shared native modal overlay", () => {
        useCallStore.getState().setIncomingCall(makeCall());

        render(<IncomingCallModal />);

        expect(mockModalOverlay).toHaveBeenCalledWith(
            expect.objectContaining({
                visible: true,
                backdropClassName: expect.stringContaining("bg-black/70"),
                contentClassName: expect.stringContaining("bg-app-card"),
            }),
        );
        expect(screen.getAllByText("Боб").length).toBeGreaterThan(0);
    });

    it("accepts an incoming call and opens the call screen", async () => {
        const acceptedCall = makeCall({ status: "Active" });
        mockCalls.accept.mockResolvedValue(acceptedCall);
        useCallStore.getState().setIncomingCall(makeCall());

        render(<IncomingCallModal />);

        fireEvent.press(screen.getByText("Принять"));

        await waitFor(() => {
            expect(mockEnsureCallMediaAccess).toHaveBeenCalledWith(
                expect.objectContaining({ callType: "Audio" }),
            );
            expect(mockCalls.accept).toHaveBeenCalledWith("call-1");
            expect(mockStopCallRingtone).toHaveBeenCalledWith("incoming");
            expect(mockRouter.push).toHaveBeenCalledWith("/call/call-1");
        });
    });

    it("does not accept when microphone preflight fails", async () => {
        mockEnsureCallMediaAccess.mockResolvedValueOnce({
            ok: false,
            errorMessage: "Не удалось получить доступ к микрофону. Проверьте разрешения браузера.",
        });
        useCallStore.getState().setIncomingCall(makeCall());

        render(<IncomingCallModal />);

        fireEvent.press(screen.getByText("Принять"));

        await waitFor(() => {
            expect(screen.getByText("Не удалось получить доступ к микрофону. Проверьте разрешения браузера.")).toBeTruthy();
        });
        expect(mockCalls.accept).not.toHaveBeenCalled();
        expect(mockRouter.push).not.toHaveBeenCalled();
    });
});
