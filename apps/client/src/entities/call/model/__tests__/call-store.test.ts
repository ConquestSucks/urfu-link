import type { CallSessionDto, CallTokenDto } from "@urfu-link/api-client";

const mockCalls = {
    start: jest.fn(),
    accept: jest.fn(),
    decline: jest.fn(),
    cancel: jest.fn(),
    leave: jest.fn(),
    get: jest.fn(),
    token: jest.fn(),
};

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        calls: mockCalls,
    },
}));

const { useCallStore } = require("../call-store");

const now = "2026-05-30T10:00:00.000Z";

const makeCall = (overrides: Partial<CallSessionDto> = {}): CallSessionDto => ({
    id: "call-1",
    conversationId: "conversation-1",
    callerId: "user-1",
    participantIds: ["user-1", "user-2"],
    callType: "Audio",
    status: "Ringing",
    createdAtUtc: now,
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

const makeActiveCall = (overrides: Partial<CallSessionDto> = {}): CallSessionDto =>
    makeCall({
        status: "Active",
        acceptedAtUtc: now,
        participants: [
            { userId: "user-1", isConnected: true },
            { userId: "user-2", isConnected: true },
        ],
        ...overrides,
    });

const makeEndedCall = (overrides: Partial<CallSessionDto> = {}): CallSessionDto =>
    makeCall({
        status: "Ended",
        endedAtUtc: "2026-05-30T10:05:00.000Z",
        endReason: "Completed",
        ...overrides,
    });

const makeToken = (overrides: Partial<CallTokenDto> = {}): CallTokenDto => ({
    callId: "call-1",
    serverUrl: "wss://livekit.test",
    roomName: "call-call-1",
    token: "jwt-token",
    expiresAtUtc: "2026-05-30T10:10:00.000Z",
    ...overrides,
});

describe("useCallStore", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        jest.spyOn(Date, "now").mockReturnValue(Date.parse(now));
        useCallStore.getState().clearCallCache();
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it("stores incoming calls", () => {
        const call = makeCall();

        useCallStore.getState().handleIncomingCall(call);

        expect(useCallStore.getState().incomingCall).toEqual(call);
        expect(useCallStore.getState().activeCall).toBeNull();
    });

    it("accept event clears matching incoming call and sets active call", () => {
        const ringing = makeCall();
        const active = makeActiveCall();
        useCallStore.getState().handleIncomingCall(ringing);

        useCallStore.getState().handleCallAccepted(active);

        expect(useCallStore.getState().incomingCall).toBeNull();
        expect(useCallStore.getState().activeCall).toEqual(active);
    });

    it.each([
        ["declined", "handleCallDeclined"],
        ["cancelled", "handleCallCancelled"],
        ["ended", "handleCallEnded"],
    ] as const)("%s event clears matching incoming call", (_label, handlerName) => {
        const ringing = makeCall();
        const ended = makeEndedCall();
        useCallStore.getState().handleIncomingCall(ringing);

        useCallStore.getState()[handlerName](ended);

        expect(useCallStore.getState().incomingCall).toBeNull();
    });

    it("participant joined and left events update active call connection state", () => {
        useCallStore.getState().setActiveCall(makeActiveCall({
            participants: [
                { userId: "user-1", isConnected: true },
                { userId: "user-2", isConnected: false },
            ],
        }));

        useCallStore.getState().handleCallParticipantJoined("call-1", "user-2");
        expect(useCallStore.getState().activeCall?.participants).toContainEqual({
            userId: "user-2",
            isConnected: true,
        });

        useCallStore.getState().handleCallParticipantLeft("call-1", "user-2");
        expect(useCallStore.getState().activeCall?.participants).toContainEqual({
            userId: "user-2",
            isConnected: false,
        });
    });

    it("startCall calls the API and stores the outgoing ringing call", async () => {
        const ringing = makeCall({ callType: "Video" });
        mockCalls.start.mockResolvedValue(ringing);

        const result = await useCallStore.getState().startCall("conversation-1", "Video");

        expect(mockCalls.start).toHaveBeenCalledWith("conversation-1", "Video");
        expect(result).toEqual(ringing);
        expect(useCallStore.getState().outgoingCall).toEqual(ringing);
        expect(useCallStore.getState().activeCall).toBeNull();
        expect(useCallStore.getState().incomingCall).toBeNull();
    });

    it("accept event promotes a matching outgoing call to active", () => {
        const ringing = makeCall();
        const active = makeActiveCall();
        useCallStore.getState().setOutgoingCall(ringing);

        useCallStore.getState().handleCallAccepted(active);

        expect(useCallStore.getState().outgoingCall).toBeNull();
        expect(useCallStore.getState().activeCall).toEqual(active);
    });

    it.each([
        ["declined", "handleCallDeclined"],
        ["cancelled", "handleCallCancelled"],
        ["ended", "handleCallEnded"],
    ] as const)("%s event clears matching outgoing call", (_label, handlerName) => {
        const ringing = makeCall();
        const ended = makeEndedCall();
        useCallStore.getState().setOutgoingCall(ringing);

        useCallStore.getState()[handlerName](ended);

        expect(useCallStore.getState().outgoingCall).toBeNull();
    });

    it("cancelCall clears a matching outgoing call", async () => {
        const ringing = makeCall();
        const ended = makeEndedCall({ endReason: "CancelledByCaller" });
        mockCalls.cancel.mockResolvedValue(ended);
        useCallStore.getState().setOutgoingCall(ringing);

        await useCallStore.getState().cancelCall("call-1");

        expect(mockCalls.cancel).toHaveBeenCalledWith("call-1");
        expect(useCallStore.getState().outgoingCall).toBeNull();
    });

    it("acceptIncoming accepts through the API and promotes active call", async () => {
        const ringing = makeCall();
        const active = makeActiveCall();
        mockCalls.accept.mockResolvedValue(active);
        useCallStore.getState().setIncomingCall(ringing);

        await useCallStore.getState().acceptIncoming();

        expect(mockCalls.accept).toHaveBeenCalledWith("call-1");
        expect(useCallStore.getState().incomingCall).toBeNull();
        expect(useCallStore.getState().activeCall).toEqual(active);
    });

    it("declineIncoming declines through the API and clears incoming call", async () => {
        const ringing = makeCall();
        mockCalls.decline.mockResolvedValue(makeEndedCall({ endReason: "DeclinedByCallee" }));
        useCallStore.getState().setIncomingCall(ringing);

        await useCallStore.getState().declineIncoming();

        expect(mockCalls.decline).toHaveBeenCalledWith("call-1");
        expect(useCallStore.getState().incomingCall).toBeNull();
    });

    it("loadToken caches fresh tokens", async () => {
        const token = makeToken();
        mockCalls.token.mockResolvedValue(token);

        const first = await useCallStore.getState().loadToken("call-1");
        const second = await useCallStore.getState().loadToken("call-1");

        expect(first).toEqual(token);
        expect(second).toEqual(token);
        expect(mockCalls.token).toHaveBeenCalledTimes(1);
        expect(useCallStore.getState().tokenLoadingByCallId["call-1"]).toBe(false);
        expect(useCallStore.getState().tokenErrorByCallId["call-1"]).toBeNull();
    });

    it("loadToken records error state and clears loading on failure", async () => {
        mockCalls.token.mockRejectedValue(new Error("token failed"));

        await expect(useCallStore.getState().loadToken("call-1")).rejects.toThrow("token failed");

        expect(useCallStore.getState().tokenLoadingByCallId["call-1"]).toBe(false);
        expect(useCallStore.getState().tokenErrorByCallId["call-1"]).toBe("token failed");
    });

    it("clearToken and clearCallCache remove token and call state", async () => {
        mockCalls.token.mockResolvedValue(makeToken());
        useCallStore.getState().setIncomingCall(makeCall());
        useCallStore.getState().setOutgoingCall(makeCall());
        useCallStore.getState().setActiveCall(makeActiveCall());
        await useCallStore.getState().loadToken("call-1");

        useCallStore.getState().clearToken("call-1");

        expect(useCallStore.getState().callTokens["call-1"]).toBeUndefined();
        expect(useCallStore.getState().tokenErrorByCallId["call-1"]).toBeNull();

        useCallStore.getState().clearCallCache();

        expect(useCallStore.getState().incomingCall).toBeNull();
        expect(useCallStore.getState().outgoingCall).toBeNull();
        expect(useCallStore.getState().activeCall).toBeNull();
        expect(useCallStore.getState().callTokens).toEqual({});
        expect(useCallStore.getState().tokenLoadingByCallId).toEqual({});
        expect(useCallStore.getState().tokenErrorByCallId).toEqual({});
    });
});
