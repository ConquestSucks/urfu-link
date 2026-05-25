import { AuthHeaders, HandleUnauthorized, createRequest } from "./utils";

const PREFIX = "/api/notifications";

export type NotificationSeverity = 0 | 1 | 2 | 3;

export type NotificationActorDto = {
  id: string | null;
  displayName: string | null;
  avatarUrl: string | null;
};

export type NotificationEntityDto = {
  kind: string;
  id: string;
  displayName: string | null;
};

export type NotificationActionDto = {
  id: string;
  label: string;
  kind: string;
  deepLink: string | null;
};

export type NotificationDto = {
  id: string;
  recipientUserId: string;
  type: string;
  category: number;
  severity: NotificationSeverity;
  title: string;
  body: string;
  imageUrl: string | null;
  deepLink: string | null;
  data: Record<string, string>;
  actor: NotificationActorDto | null;
  entity: NotificationEntityDto | null;
  actions: NotificationActionDto[];
  groupKey: string | null;
  occurrenceCount: number;
  createdAtUtc: string;
  lastOccurrenceAtUtc: string;
  readAtUtc: string | null;
  seenAtUtc: string | null;
  savedAtUtc: string | null;
  doneAtUtc: string | null;
  archivedAtUtc: string | null;
  snoozedUntilUtc: string | null;
  expiresAtUtc: string | null;
};

export type NotificationBadgeDto = {
  total: number;
  perCategory: Record<number, number>;
  totalUnseen: number;
  urgentUnread: number;
  perType: Record<string, number> | null;
};

export type NotificationListStatus =
  | "all"
  | "unread"
  | "read"
  | "seen"
  | "unseen"
  | "saved"
  | "done"
  | "archived";

export type ListNotificationsParams = {
  cursor?: string;
  limit?: number;
  category?: number;
  status?: NotificationListStatus;
  type?: string;
  severity?: NotificationSeverity;
  query?: string;
  from?: string;
  to?: string;
};

export type ListNotificationsResponse = {
  items: NotificationDto[];
  nextCursor?: string | null;
};

export type BulkNotificationFilter = Omit<ListNotificationsParams, "cursor" | "limit">;

export type BulkNotificationAction = {
  action: "read" | "unread" | "seen" | "save" | "unsave" | "done" | "restore" | "archive";
  ids?: string[];
  filter?: BulkNotificationFilter;
};

export type BulkNotificationActionResponse = {
  updated: number;
};

export function createNotificationsApi(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized,
) {
  const request = createRequest(baseUrl, authHeaders, handleUnauthorized);

  const stateAction = (id: string, action: BulkNotificationAction["action"]) =>
    request<void>(`${PREFIX}/me/notifications/${encodeURIComponent(id)}/${action}`, {
      method: "POST",
    });

  return {
    list(params: ListNotificationsParams = {}): Promise<ListNotificationsResponse> {
      const query = new URLSearchParams();
      if (params.limit) query.append("limit", String(params.limit));
      if (params.cursor) query.append("cursor", params.cursor);
      if (params.status) query.append("status", params.status);
      if (params.type) query.append("type", params.type);
      if (params.severity !== undefined) query.append("severity", String(params.severity));
      if (params.query) query.append("query", params.query);
      if (params.category !== undefined) query.append("category", String(params.category));
      if (params.from) query.append("from", params.from);
      if (params.to) query.append("to", params.to);

      const suffix = query.toString();
      return request<ListNotificationsResponse>(
        `${PREFIX}/me/notifications${suffix ? `?${suffix}` : ""}`,
      );
    },

    get(id: string): Promise<NotificationDto> {
      return request<NotificationDto>(`${PREFIX}/me/notifications/${encodeURIComponent(id)}`);
    },

    getBadge(): Promise<NotificationBadgeDto> {
      return request<NotificationBadgeDto>(`${PREFIX}/me/notifications/badge`);
    },

    markRead(id: string): Promise<void> {
      return stateAction(id, "read");
    },

    markUnread(id: string): Promise<void> {
      return stateAction(id, "unread");
    },

    markSeen(id: string): Promise<void> {
      return stateAction(id, "seen");
    },

    save(id: string): Promise<void> {
      return stateAction(id, "save");
    },

    unsave(id: string): Promise<void> {
      return stateAction(id, "unsave");
    },

    markDone(id: string): Promise<void> {
      return stateAction(id, "done");
    },

    restore(id: string): Promise<void> {
      return stateAction(id, "restore");
    },

    bulk(action: BulkNotificationAction): Promise<BulkNotificationActionResponse> {
      return request<BulkNotificationActionResponse>(`${PREFIX}/me/notifications/bulk`, {
        method: "POST",
        body: JSON.stringify(action),
      });
    },
  };
}
