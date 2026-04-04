export type BackendHealth = {
  ok: boolean;
  status: number;
  body: string;
  checkedAt: string;
};

export type {
  UserProfile,
  UserIdentity,
  UserAccount,
  UserPrivacy,
  UserNotifications,
  UserSoundVideo,
  DeviceSession,
  UpdateAccountDto,
  UpdatePrivacyDto,
  UpdateNotificationsDto,
  UpdateSoundVideoDto,
} from "./users";

import { createUsersApi } from "./users";

type ApiClientConfig = {
  baseUrl: string;
  getAccessToken?: () => string | undefined;
  onUnauthorized?: () => void;
};

export function createApiClient({ baseUrl, getAccessToken, onUnauthorized }: ApiClientConfig) {
  const normalizedBaseUrl = baseUrl.replace(/\/$/, "");

  function authHeaders(): Record<string, string> {
    const token = getAccessToken?.();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  let redirecting = false;
  function handleUnauthorized(response: Response): void {
    if (response.status !== 401) return;
    if (onUnauthorized) {
      onUnauthorized();
      return;
    }
    if (typeof window !== "undefined" && !redirecting) {
      redirecting = true;
      const rd = encodeURIComponent(window.location.href);
      const isRevoked = response.headers.get("X-Session-Revoked") === "true";
      window.location.href = isRevoked
        ? `/.pomerium/sign_out?pomerium_redirect_uri=${rd}`
        : `/.pomerium/sign_in?pomerium_redirect_uri=${rd}`;
    }
  }

  return {
    users: createUsersApi(normalizedBaseUrl, authHeaders, handleUnauthorized),

    async health(): Promise<BackendHealth> {
      try {
        const response = await fetch(`${normalizedBaseUrl}/health/ready`, {
          headers: {
            Accept: "text/plain",
            ...authHeaders(),
          },
          credentials: "same-origin",
        });

        handleUnauthorized(response);

        const body = await response.text();

        return {
          ok: response.ok,
          status: response.status,
          body,
          checkedAt: new Date().toISOString()
        };
      } catch (error) {
        return {
          ok: false,
          status: 0,
          body: error instanceof Error ? error.message : "Unknown error",
          checkedAt: new Date().toISOString()
        };
      }
    }
  };
}
