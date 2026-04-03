export type UserIdentity = {
  name: string;
  email: string;
  username: string;
};

export type UserAccount = {
  avatarUrl: string | null;
  aboutMe: string | null;
};

export type UserPrivacy = {
  showOnlineStatus: boolean;
  showLastVisitTime: boolean;
};

export type UserNotifications = {
  newMessages: boolean;
  notificationSound: boolean;
  disciplineChatMessages: boolean;
  mentions: boolean;
};

export type UserSoundVideo = {
  playbackDeviceId: string | null;
  recordingDeviceId: string | null;
  webcamDeviceId: string | null;
};

export type UserProfile = {
  userId: string;
  identity: UserIdentity;
  account: UserAccount;
  privacy: UserPrivacy;
  notifications: UserNotifications;
  soundVideo: UserSoundVideo;
};

export type DeviceSession = {
  sessionId: string;
  ipAddress: string | null;
  lastAccess: string;
  browser: string | null;
  os: string | null;
  isCurrent: boolean;
};

export type UpdateAccountDto = {
  aboutMe: string | null;
};

export type UpdatePrivacyDto = {
  showOnlineStatus: boolean;
  showLastVisitTime: boolean;
};

export type UpdateNotificationsDto = {
  newMessages: boolean;
  notificationSound: boolean;
  disciplineChatMessages: boolean;
  mentions: boolean;
};

export type UpdateSoundVideoDto = {
  playbackDeviceId?: string | null;
  recordingDeviceId?: string | null;
  webcamDeviceId?: string | null;
};

type AuthHeaders = () => Record<string, string>;
type HandleUnauthorized = (response: Response) => void;

export function createUsersApi(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  async function request<T>(
    path: string,
    init?: RequestInit
  ): Promise<T> {
    const response = await fetch(`${baseUrl}${path}`, {
      ...init,
      headers: {
        ...(init?.body instanceof FormData ? {} : { "Content-Type": "application/json" }),
        ...authHeaders(),
        ...init?.headers,
      },
      credentials: "same-origin",
    });

    handleUnauthorized(response);

    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return response.json() as Promise<T>;
  }

  return {
    getMe(): Promise<UserProfile> {
      return request<UserProfile>("/api/users/me");
    },

    updateAccount(dto: UpdateAccountDto): Promise<void> {
      return request<void>("/api/users/me/account", {
        method: "PUT",
        body: JSON.stringify(dto),
      });
    },

    uploadAvatar(file: File): Promise<{ avatarUrl: string }> {
      const form = new FormData();
      form.append("file", file);
      return request<{ avatarUrl: string }>("/api/users/me/avatar", {
        method: "PUT",
        body: form,
        headers: authHeaders(), // skip Content-Type — browser sets multipart boundary
      });
    },

    deleteAvatar(): Promise<void> {
      return request<void>("/api/users/me/avatar", { method: "DELETE" });
    },

    updatePrivacy(dto: UpdatePrivacyDto): Promise<void> {
      return request<void>("/api/users/me/privacy", {
        method: "PUT",
        body: JSON.stringify(dto),
      });
    },

    updateNotifications(dto: UpdateNotificationsDto): Promise<void> {
      return request<void>("/api/users/me/notifications", {
        method: "PUT",
        body: JSON.stringify(dto),
      });
    },

    updateSoundVideo(dto: UpdateSoundVideoDto): Promise<void> {
      return request<void>("/api/users/me/sound-video", {
        method: "PATCH",
        body: JSON.stringify(dto),
      });
    },

    getDevices(): Promise<DeviceSession[]> {
      return request<DeviceSession[]>("/api/users/me/devices");
    },

    terminateDevice(sessionId: string): Promise<void> {
      return request<void>(`/api/users/me/devices/${encodeURIComponent(sessionId)}`, {
        method: "DELETE",
      });
    },

    terminateAllDevices(): Promise<void> {
      return request<void>("/api/users/me/devices", { method: "DELETE" });
    },
  };
}
