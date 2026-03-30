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
} from "./users";

import { createUsersApi } from "./users";

type ApiClientConfig = {
  baseUrl: string;
  getAccessToken?: () => string | undefined;
};

export function createApiClient({ baseUrl, getAccessToken }: ApiClientConfig) {
  const normalizedBaseUrl = baseUrl.replace(/\/$/, "");

  function authHeaders(): Record<string, string> {
    const token = getAccessToken?.();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  let redirecting = false;
  function handleUnauthorized(response: Response): void {
    if (response.status === 401 && typeof window !== "undefined" && !redirecting) {
      redirecting = true;
      const rd = encodeURIComponent(window.location.href);
      window.location.href = `/.pomerium/sign_in?pomerium_redirect_uri=${rd}`;
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
