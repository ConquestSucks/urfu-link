import { create } from "zustand";
import {
    type CallSessionDto,
    type CallTokenDto,
    type CallType,
} from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";

const tokenRefreshMarginMs = 15_000;

type TokenByCallId = Record<string, CallTokenDto>;
type LoadingByCallId = Record<string, boolean>;
type ErrorByCallId = Record<string, string | null>;

const toBoolean = (value: unknown) => !!value;

const cloneTokenByCallId = (source: TokenByCallId): TokenByCallId => ({ ...source });

const mergeParticipants = (
    participants: { userId: string; isConnected: boolean }[] | undefined,
    userId: string,
    isConnected: boolean,
) => {
    const list = [...(participants ?? [])];
    const index = list.findIndex((p) => p.userId === userId);

    if (index >= 0) {
        list[index] = { ...list[index], isConnected };
        return list;
    }

    list.push({ userId, isConnected });
    return list;
};

const isTokenFresh = (token?: CallTokenDto | null): boolean => {
    if (!token?.expiresAtUtc) return false;

    const expiration = Date.parse(token.expiresAtUtc);
    return Number.isFinite(expiration) && expiration - Date.now() > tokenRefreshMarginMs;
};

export type CallStoreState = {
    incomingCall: CallSessionDto | null;
    activeCall: CallSessionDto | null;
    callTokens: TokenByCallId;
    tokenLoadingByCallId: LoadingByCallId;
    tokenErrorByCallId: ErrorByCallId;

    setIncomingCall: (call: CallSessionDto | null) => void;
    setActiveCall: (call: CallSessionDto | null) => void;
    clearIncomingCall: () => void;
    clearActiveCall: () => void;

    handleIncomingCall: (call: CallSessionDto) => void;
    handleCallAccepted: (call: CallSessionDto) => void;
    handleCallDeclined: (call: CallSessionDto) => void;
    handleCallCancelled: (call: CallSessionDto) => void;
    handleCallEnded: (call: CallSessionDto) => void;
    handleCallParticipantJoined: (callId: string, userId: string) => void;
    handleCallParticipantLeft: (callId: string, userId: string) => void;

    startCall: (conversationId: string, callType: CallType) => Promise<CallSessionDto>;
    acceptIncoming: () => Promise<void>;
    declineIncoming: () => Promise<void>;
    cancelCall: (callId: string) => Promise<void>;
    leaveCall: (callId: string) => Promise<void>;
    loadCall: (callId: string) => Promise<CallSessionDto>;
    loadToken: (callId: string) => Promise<CallTokenDto>;
    clearToken: (callId: string) => void;
    clearCallCache: () => void;
};

export const useCallStore = create<CallStoreState>((set, get) => ({
    incomingCall: null,
    activeCall: null,
    callTokens: {},
    tokenLoadingByCallId: {},
    tokenErrorByCallId: {},

    setIncomingCall: (call) =>
        set({ incomingCall: call, activeCall: call?.status === "Active" ? call : get().activeCall }),

    setActiveCall: (call) => set({
        activeCall: call,
        incomingCall: call && get().incomingCall?.id === call.id ? null : get().incomingCall,
    }),

    clearIncomingCall: () => set({ incomingCall: null }),

    clearActiveCall: () => set({ activeCall: null }),

    handleIncomingCall: (call) =>
        set((state) => ({
            incomingCall: call,
            activeCall:
                state.activeCall && state.activeCall.id === call.id
                    ? call
                    : state.activeCall,
        })),

    handleCallAccepted: (call) =>
        set((state) => ({
            incomingCall: state.incomingCall?.id === call.id ? null : state.incomingCall,
            activeCall: state.activeCall?.id === call.id || state.incomingCall?.id === call.id
                ? call
                : state.activeCall,
        })),

    handleCallDeclined: (call) =>
        set((state) => ({
            incomingCall: state.incomingCall?.id === call.id ? null : state.incomingCall,
            activeCall: state.activeCall?.id === call.id ? call : state.activeCall,
        })),

    handleCallCancelled: (call) =>
        set((state) => ({
            incomingCall: state.incomingCall?.id === call.id ? null : state.incomingCall,
            activeCall: state.activeCall?.id === call.id ? call : state.activeCall,
        })),

    handleCallEnded: (call) =>
        set((state) => ({
            incomingCall: state.incomingCall?.id === call.id ? null : state.incomingCall,
            activeCall: state.activeCall?.id === call.id ? call : state.activeCall,
        })),

    handleCallParticipantJoined: (callId, userId) =>
        set((state) => {
            if (!state.activeCall || state.activeCall.id !== callId) {
                return state;
            }

            return {
                activeCall: {
                    ...state.activeCall,
                    participants: mergeParticipants(
                        state.activeCall.participants,
                        userId,
                        true,
                    ),
                },
            };
        }),

    handleCallParticipantLeft: (callId, userId) =>
        set((state) => {
            if (!state.activeCall || state.activeCall.id !== callId) {
                return state;
            }

            return {
                activeCall: {
                    ...state.activeCall,
                    participants: mergeParticipants(
                        state.activeCall.participants,
                        userId,
                        false,
                    ),
                },
            };
        }),

    startCall: async (conversationId, callType) => {
        const call = await apiClient.calls.start(conversationId, callType);
        set({
            incomingCall: null,
            activeCall: call,
        });
        return call;
    },

    acceptIncoming: async () => {
        const incoming = get().incomingCall;
        if (!incoming) return;

        const call = await apiClient.calls.accept(incoming.id);
        set({
            incomingCall: null,
            activeCall: call,
        });
    },

    declineIncoming: async () => {
        const incoming = get().incomingCall;
        if (!incoming) return;

        await apiClient.calls.decline(incoming.id);
        set({ incomingCall: null });
    },

    cancelCall: async (callId) => {
        if (!callId) return;

        const call = await apiClient.calls.cancel(callId);
        set((state) => ({
            activeCall: state.activeCall?.id === call.id ? call : state.activeCall,
            incomingCall: state.incomingCall?.id === call.id ? null : state.incomingCall,
        }));
    },

    leaveCall: async (callId) => {
        if (!callId) return;

        const call = await apiClient.calls.leave(callId);
        set((state) => ({
            activeCall: state.activeCall?.id === call.id ? call : state.activeCall,
        }));
    },

    loadCall: async (callId) => {
        const call = await apiClient.calls.get(callId);
        set((state) => {
            const sameIdIncoming = state.incomingCall && state.incomingCall.id === call.id;
            const sameIdActive = state.activeCall && state.activeCall.id === call.id;
            return {
                incomingCall: toBoolean(sameIdIncoming) ? call : state.incomingCall,
                activeCall: toBoolean(sameIdActive) || !state.activeCall
                    ? call
                    : state.activeCall,
            };
        });
        return call;
    },

    loadToken: async (callId) => {
        const cached = get().callTokens[callId];
        if (isTokenFresh(cached)) {
            return cached;
        }

        set((state) => ({
            tokenLoadingByCallId: {
                ...state.tokenLoadingByCallId,
                [callId]: true,
            },
            tokenErrorByCallId: {
                ...state.tokenErrorByCallId,
                [callId]: null,
            },
        }));

        try {
            const token = await apiClient.calls.token(callId);
            set((state) => ({
                callTokens: {
                    ...state.callTokens,
                    [callId]: token,
                },
                tokenLoadingByCallId: {
                    ...state.tokenLoadingByCallId,
                    [callId]: false,
                },
            }));
            return token;
        } catch (error) {
            set((state) => ({
                tokenErrorByCallId: {
                    ...state.tokenErrorByCallId,
                    [callId]: error instanceof Error ? error.message : "Не удалось получить токен",
                },
                tokenLoadingByCallId: {
                    ...state.tokenLoadingByCallId,
                    [callId]: false,
                },
            }));

            throw error;
        }
    },

    clearToken: (callId) =>
        set((state) => {
            const next = cloneTokenByCallId(state.callTokens);
            delete next[callId];

            return {
                callTokens: next,
                tokenErrorByCallId: {
                    ...state.tokenErrorByCallId,
                    [callId]: null,
                },
            };
        }),

    clearCallCache: () =>
        set({
            incomingCall: null,
            activeCall: null,
            callTokens: {},
            tokenLoadingByCallId: {},
            tokenErrorByCallId: {},
        }),
}));
