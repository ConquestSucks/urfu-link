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

    getBatchUserPresence(userIds: string[]): Promise<PresenceInfo[]> {
      return request<PresenceInfo[]>("/api/presence/users/batch", {
        method: "POST",
        body: JSON.stringify({ userIds }),
      });
    }
  };
}
