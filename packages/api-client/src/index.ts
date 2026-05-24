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
  SearchUserDto,
  SearchUsersResponse,
} from "./users";

export type {
  ConversationType,
  ConversationPreview,
  ConversationPreviewSnippet,
  MessageState,
  AttachmentType,
  ChatAttachment,
  ReplyToDto,
  ForwardedFromDto,
  ReactionsSummary,
  DeleteMode,
  MessageDto,
  Paginated,
  SearchResultDto,
  SearchFilters,
  ReadReceiptDto,
  ThreadSubscriptionReason,
  ActiveThreadDto,
  ParticipantRole,
  ConversationParticipantDto,
} from "./chat";

export type {
  Visibility,
  InitUploadRequest,
  InitUploadResponse,
  CompleteUploadRequest,
  AssetMetadata,
} from "./media";

export type {
  PresenceStatus,
  Platform,
  PresenceInfo,
} from "./presence";

import { createUsersApi } from "./users";
import { createChatApi } from "./chat";
import { createMediaApi } from "./media";
import { createPresenceApi } from "./presence";

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
    const isAuthRedirect = response.type === "opaqueredirect";
    if (response.status !== 401 && !isAuthRedirect) return;
    if (onUnauthorized) {
      onUnauthorized();
      return;
    }
    if (typeof window !== "undefined" && !redirecting) {
      redirecting = true;
      const isRevoked = !isAuthRedirect && response.headers.get("X-Session-Revoked") === "true";
      if (isRevoked) {
        const rd = encodeURIComponent(window.location.href);
        window.location.href = `/.pomerium/sign_out?pomerium_redirect_uri=${rd}`;
        return;
      }

      // Let Pomerium generate its signed sign-in URL instead of constructing
      // /.pomerium/sign_in directly in the SPA.
      window.location.assign(window.location.href);
    }
  }

  return {
    users: createUsersApi(normalizedBaseUrl, authHeaders, handleUnauthorized),
    chat: createChatApi(normalizedBaseUrl, authHeaders, handleUnauthorized),
    media: createMediaApi(normalizedBaseUrl, authHeaders, handleUnauthorized),
    presence: createPresenceApi(normalizedBaseUrl, authHeaders, handleUnauthorized),

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
