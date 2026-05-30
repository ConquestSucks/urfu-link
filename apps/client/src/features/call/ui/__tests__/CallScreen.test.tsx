import React from "react";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { render, screen } from "@testing-library/react-native";
import type { CallSessionDto, CallTokenDto } from "@urfu-link/api-client";

const mockReplace = jest.fn();
const mockLoadCall = jest.fn();
const mockSetActiveCall = jest.fn();
const mockClearToken = jest.fn();
const mockLoadToken = jest.fn();
const mockLeaveCall = jest.fn();
const mockCancelCall = jest.fn();
const mockLoadParticipants = jest.fn();

const mockCall: CallSessionDto = {
    id: "call-123",
    conversationId: "conversation-1",
    callerId: "user-1",
    participantIds: ["user-1", "user-2"],
    callType: "Video",
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
};

const mockToken: CallTokenDto = {
    callId: "call-123",
    serverUrl: "wss://livekit.test",
    roomName: "call-call-123",
    token: "jwt-token",
    expiresAtUtc: "2026-05-30T10:10:00.000Z",
};

const mockCallState = {
    loadCall: mockLoadCall,
    setActiveCall: mockSetActiveCall,
    clearToken: mockClearToken,
    loadToken: mockLoadToken,
    leaveCall: mockLeaveCall,
    cancelCall: mockCancelCall,
    incomingCall: null,
    activeCall: mockCall,
    callTokens: {
        [mockCall.id]: mockToken,
    },
    tokenLoadingByCallId: {},
    tokenErrorByCallId: {},
};

jest.mock("expo-router", () => ({
    useLocalSearchParams: () => ({ id: "call-123" }),
    useRouter: () => ({
        replace: mockReplace,
    }),
}));

jest.mock("@/shared/lib/useWindowSize", () => ({
    useWindowSize: () => ({ width: 1280, height: 720, isMobile: false }),
}));

jest.mock("@/shared/store/auth-store", () => ({
    useCurrentUserId: () => "user-1",
}));

jest.mock("@/entities/user", () => ({
    useCurrentUser: () => ({
        data: {
            userId: "user-1",
            identity: {
                name: "Current User",
                email: "current@example.test",
                username: "current",
            },
            account: {
                avatarUrl: "https://cdn.test/current.png",
                aboutMe: null,
            },
        },
    }),
}));

jest.mock("@/entities/call", () => ({
    useCallStore: (selector: (state: typeof mockCallState) => unknown) =>
        selector(mockCallState),
}));

jest.mock("@/entities/conversation/model/chat-store", () => ({
    useChatStore: (
        selector: (state: { conversations: { id: string; title: string }[] }) => unknown,
    ) =>
        selector({
            conversations: [{ id: "conversation-1", title: "Test conversation" }],
        }),
}));

jest.mock("@/entities/conversation/model/participants-store", () => ({
    useConversationParticipants: () => [
        { userId: "user-1", displayName: "Current User", avatarUrl: "" },
        { userId: "user-2", displayName: "Second User", avatarUrl: "https://cdn.test/second.png" },
    ],
    useParticipantsStore: {
        getState: () => ({ load: mockLoadParticipants }),
    },
}));

jest.mock("../CallRoom", () => ({
    CallRoom: ({
        call,
        participantInfos,
    }: {
        call: CallSessionDto;
        participantInfos: Array<{ userId: string; displayName: string; avatarUrl?: string | null; isSelf: boolean }>;
    }) => {
        const { Text } = require("react-native");
        const self = participantInfos.find((participant) => participant.isSelf);
        return (
            <>
                <Text>LiveKit room for {call.id}</Text>
                <Text>{`self:${self?.displayName}:${self?.avatarUrl}`}</Text>
            </>
        );
    },
}));

const { CallScreen } = require("../CallScreen.web");

describe("CallScreen", () => {
    beforeEach(() => {
        jest.clearAllMocks();
    });

    it("keeps the web entrypoint wired to the shared call screen", () => {
        const webEntrypoint = join(__dirname, "../CallScreen.web.tsx");

        expect(existsSync(webEntrypoint)).toBe(true);
        expect(require("node:fs").readFileSync(webEntrypoint, "utf8")).not.toContain(
            "Звонки в веб-версии временно недоступны",
        );
    });

    it("renders the platform call room instead of the old web fallback", () => {
        render(<CallScreen />);

        expect(screen.getByText("LiveKit room for call-123")).toBeTruthy();
        expect(
            screen.queryByText("Звонки в веб-версии временно недоступны"),
        ).toBeNull();
    });

    it("renders the current user like a normal participant with profile avatar fallback", () => {
        render(<CallScreen />);

        expect(screen.getByText("self:Current User:https://cdn.test/current.png")).toBeTruthy();
        expect(screen.queryByText("self:Вы:https://cdn.test/current.png")).toBeNull();
    });
});
