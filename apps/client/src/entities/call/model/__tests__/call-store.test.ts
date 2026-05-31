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

jest.mock("../call-client-instance", () => ({
    getCallClientInstanceId: jest.fn(() => "client-1"),
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
        Object.defineProperty(globalThis, "localStorage", {
            configurable: true,
            value: {
                getItem: jest.fn(() => null),
                setItem: jest.fn(),
                removeItem: jest.fn(),
            },
        });
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

    it("participant joined and left events update active group call connection state", () => {
        useCallStore.getState().setActiveCall(makeActiveCall({
            conversationId: "discipline:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            participantIds: ["user-1", "user-2", "user-3"],
            participants: [
                { userId: "user-1", isConnected: true },
                { userId: "user-2", isConnected: false },
                { userId: "user-3", isConnected: false },
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
        expect(useCallStore.getState().activeCall?.status).toBe("Active");
    });

    it("participant left event completes an active direct call locally", () => {
        useCallStore.getState().setActiveCall(makeActiveCall());

        useCallStore.getState().handleCallParticipantLeft("call-1", "user-2");

        expect(useCallStore.getState().activeCall).toEqual(
            expect.objectContaining({
                status: "Ended",
                endedAtUtc: now,
                endReason: "Completed",
            }),
        );
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

    it("startCall stores active group calls without outgoing ringtone state", async () => {
        const active = makeActiveCall({
            conversationId: "discipline:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            participantIds: ["user-1", "user-2", "user-3"],
            participants: [
                { userId: "user-1", isConnected: true },
                { userId: "user-2", isConnected: false },
                { userId: "user-3", isConnected: false },
            ],
        });
        mockCalls.start.mockResolvedValue(active);

        const result = await useCallStore.getState().startCall(
            "discipline:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "Video",
        );

        expect(result).toEqual(active);
        expect(useCallStore.getState().activeCall).toEqual(active);
        expect(useCallStore.getState().outgoingCall).toBeNull();
    });

    it("uses the active call from another tab instead of creating a delayed outgoing call", async () => {
        const active = makeActiveCall({ id: "call-remote" });
        (globalThis.localStorage.getItem as jest.Mock).mockReturnValue(JSON.stringify({
            callId: "call-remote",
            clientInstanceId: "client-2",
            updatedAt: Date.now(),
        }));
        mockCalls.get.mockResolvedValue(active);

        const result = await useCallStore.getState().startCall("conversation-1", "Audio");

        expect(mockCalls.start).not.toHaveBeenCalled();
        expect(mockCalls.get).toHaveBeenCalledWith("call-remote");
        expect(result).toEqual(active);
        expect(useCallStore.getState().activeCall).toEqual(active);
        expect(useCallStore.getState().outgoingCall).toBeNull();
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

    it("joinCall accepts an active group call and stores it as active", async () => {
        const active = makeActiveCall({
            id: "group-call",
            conversationId: "discipline:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        });
        mockCalls.accept.mockResolvedValue(active);

        await useCallStore.getState().joinCall("group-call");

        expect(mockCalls.accept).toHaveBeenCalledWith("group-call");
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
        expect(mockCalls.token).toHaveBeenCalledWith("call-1", "client-1");
        expect(useCallStore.getState().tokenLoadingByCallId["call-1"]).toBe(false);
        expect(useCallStore.getState().tokenErrorByCallId["call-1"]).toBeNull();
    });

    it("device replacement clears local token and blocks reconnect from old tab", async () => {
        mockCalls.token.mockResolvedValue(makeToken());
        await useCallStore.getState().loadToken("call-1");
        useCallStore.getState().setActiveCall(makeActiveCall());

        useCallStore.getState().handleCallDeviceReplaced("call-1", "client-2");

        expect(useCallStore.getState().callTokens["call-1"]).toBeUndefined();
        expect(useCallStore.getState().activeCall).toBeNull();
        expect(useCallStore.getState().tokenErrorByCallId["call-1"]).toBe(
            "Звонок открыт в другой вкладке или на другом устройстве.",
        );
        await expect(useCallStore.getState().loadToken("call-1")).rejects.toThrow(
            "Звонок открыт в другой вкладке или на другом устройстве.",
        );
        expect(mockCalls.token).toHaveBeenCalledTimes(1);
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
