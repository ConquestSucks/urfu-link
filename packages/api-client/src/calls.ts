import { AuthHeaders, HandleUnauthorized, createRequest } from "./utils";

export type CallType = "Audio" | "Video";
export type CallStatus = "Ringing" | "Active" | "Ended";
export type CallEndReason =
  | "Completed"
  | "DeclinedByCallee"
  | "CancelledByCaller"
  | "Missed"
  | "NoAnswer"
  | "Failed";

export type CallParticipantDto = {
  userId: string;
  isConnected: boolean;
};

export type CallSessionDto = {
  id: string;
  conversationId: string;
  callerId: string;
  participantIds: string[];
  callType: CallType;
  status: CallStatus;
  createdAtUtc: string;
  ringExpiresAtUtc: string;
  acceptedAtUtc?: string | null;
  endedAtUtc?: string | null;
  endReason?: CallEndReason | null;
  participants: CallParticipantDto[];
};

export type CallTokenDto = {
  callId: string;
  serverUrl: string;
  roomName: string;
  token: string;
  expiresAtUtc: string;
};

const PREFIX = "/api/calls";

export function createCallsApi(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  const request = createRequest(baseUrl, authHeaders, handleUnauthorized);

  return {
    start(conversationId: string, callType: CallType): Promise<CallSessionDto> {
      return request<CallSessionDto>(`${PREFIX}/conversations/${encodeURIComponent(conversationId)}`, {
        method: "POST",
        body: JSON.stringify({ callType }),
      });
    },

    get(callId: string): Promise<CallSessionDto> {
      return request<CallSessionDto>(`${PREFIX}/${encodeURIComponent(callId)}`);
    },

    accept(callId: string): Promise<CallSessionDto> {
      return request<CallSessionDto>(`${PREFIX}/${encodeURIComponent(callId)}/accept`, {
        method: "POST",
      });
    },

    decline(callId: string): Promise<CallSessionDto> {
      return request<CallSessionDto>(`${PREFIX}/${encodeURIComponent(callId)}/decline`, {
        method: "POST",
      });
    },

    cancel(callId: string): Promise<CallSessionDto> {
      return request<CallSessionDto>(`${PREFIX}/${encodeURIComponent(callId)}/cancel`, {
        method: "POST",
      });
    },

    leave(callId: string): Promise<CallSessionDto> {
      return request<CallSessionDto>(`${PREFIX}/${encodeURIComponent(callId)}/leave`, {
        method: "POST",
      });
    },

    token(callId: string): Promise<CallTokenDto> {
      return request<CallTokenDto>(`${PREFIX}/${encodeURIComponent(callId)}/token`, {
        method: "POST",
      });
    },
  };
}
