import { AuthHeaders, HandleUnauthorized, createRequest } from "./utils";

export type PresenceStatus = "Online" | "Away" | "DoNotDisturb" | "Invisible" | "Offline";
export type Platform = "Mobile" | "Web" | "Desktop";

export type PresenceInfo = {
  userId: string;
  status: PresenceStatus;
  platforms: Platform[];
  customActivity?: string;
  lastSeenAt?: string | null;
};

type BatchPresenceResponse = {
  items: PresenceInfo[];
};

export function createPresenceApi(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  const request = createRequest(baseUrl, authHeaders, handleUnauthorized);

  return {
    getUserPresence(userId: string): Promise<PresenceInfo> {
      return request<PresenceInfo>(`/api/presence/users/${encodeURIComponent(userId)}`);
    },

    async getBatchUserPresence(userIds: string[]): Promise<PresenceInfo[]> {
      const response = await request<BatchPresenceResponse>("/api/presence/users/batch", {
        method: "POST",
        body: JSON.stringify({ userIds }),
      });
      return response.items;
    }
  };
}
