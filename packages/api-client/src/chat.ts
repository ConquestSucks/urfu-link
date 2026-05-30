import { AuthHeaders, HandleUnauthorized, createRequest } from "./utils";

// Соответствует ChatService.Api.Domain.Enums.ConversationType (Direct=0, Group=1).
// Бэкенд сериализует enum как строку (JsonStringEnumConverter в Program.cs).
export type ConversationType = "Direct" | "Group";

// Подтип группового чата: "Discipline" для чатов учебной дисциплины.
// Соответствует ChatService.Api.Domain.Enums.GroupSubtype.
export type GroupSubtype = "Discipline";
export type DisciplineChatKind = "General" | "Subgroup";
export type ParticipantRole = "Member" | "Teacher" | "Student";

export type ConversationCapabilities = {
  canStartGroupCall: boolean;
};

export type ConversationPreview = {
  id: string;
  type: ConversationType;
  participants: string[];
  // Бэк отдаёт CreatedAtUtc / LastMessageAtUtc; для direct без сообщений
  // LastMessageAtUtc заполняется временем создания.
  createdAtUtc?: string;
  lastMessageAtUtc?: string;
  // Старые имена сохраняем как опциональные на случай fallback'а другого endpoint'а.
  createdAt?: string;
  lastMessageAt?: string | null;
  lastMessagePreview: {
    messageId?: string | null;
    senderId: string;
    body: string;
    sentAt?: string;
    sentAtUtc?: string;
    hasAttachments?: boolean;
    attachmentFileNames?: string[];
    attachmentTypes?: AttachmentType[];
    readAt?: string | null;
    readAtUtc?: string | null;
  } | null;
  pinnedMessageIds?: string[];
  title?: string;
  coverAssetId?: string | null;
  disciplineId?: string | null;
  groupSubtype?: GroupSubtype | null;
  disciplineChatKind?: DisciplineChatKind | null;
  disciplineSubgroupId?: string | null;
  disciplineTitle?: string | null;
  disciplineSubgroupName?: string | null;
  participantRoles?: Record<string, ParticipantRole> | null;
  capabilities?: ConversationCapabilities | null;
  unreadCount?: number | null;
};

export type MessageState = "Sent" | "Delivered" | "Read" | "Deleted";
export type AttachmentType = "Image" | "Video" | "Audio" | "Voice" | "Document";
export type MessageKind = "User" | "SystemCall";
export type SystemCallStatus = "Started" | "Missed" | "Completed" | "Declined" | "Cancelled" | "Failed";
export type SystemCallInfo = {
  callId: string;
  callType: "Audio" | "Video";
  status: SystemCallStatus;
  callerId: string;
  duration?: string | null;
  endReason?: string | null;
};

export type ChatAttachment = {
  mediaAssetId: string;
  type: AttachmentType;
  thumbnailAssetId?: string;
  fileName: string;
  size: number;
  mimeType: string;
  durationSeconds?: number | null;
};

export type ReplyToDto = {
  messageId: string;
  senderId: string;
  preview: string;
};

export type ForwardedFromDto = {
  originalSenderId: string;
  originalSentAtUtc: string;
  originalConversationId: string;
};

export type ReactionsSummary = Record<string, string[]>;

export type DeleteMode = "for-me" | "for-everyone";

export type MessageDto = {
  id: string;
  conversationId: string;
  senderId: string;
  body: string;
  attachments: ChatAttachment[];
  state: MessageState;
  createdAt: string;
  deliveredAt: string | null;
  readAt: string | null;
  clientMessageId?: string;
  editedAtUtc?: string | null;
  replyTo?: ReplyToDto | null;
  reactions?: ReactionsSummary;
  mentions?: string[];
  forwardedFrom?: ForwardedFromDto | null;
  threadRootId?: string | null;
  threadReplyCount?: number | null;
  threadParticipants?: string[] | null;
  threadLastReplyAtUtc?: string | null;
  deletedAtUtc?: string | null;
  deletedMode?: DeleteMode | null;
  kind?: MessageKind;
  systemCall?: SystemCallInfo | null;
};

export type Paginated<T> = {
  items: T[];
  nextCursor?: string;
};

export type ConversationPreviewSnippet = {
  type: ConversationType;
  title?: string;
  peerUserId?: string;
  avatarUrl?: string;
  senderName?: string;
};

export type SearchResultDto = {
  messageId: string;
  conversationId: string;
  conversationPreview?: ConversationPreviewSnippet;
  senderId: string;
  body: string;
  score?: number;
  createdAtUtc: string;
  highlightedSnippet?: string;
};

export type ReadReceiptDto = {
  userId: string;
  readAtUtc: string;
};

export type ThreadSubscriptionReason = "Manual" | "Mentioned" | "Replied";

export type ActiveThreadDto = {
  rootMessageId: string;
  conversationId: string;
  rootMessage: MessageDto;
  replyCount: number;
  lastActivityAtUtc: string;
  reason: ThreadSubscriptionReason;
};

export type ConversationParticipantDto = {
  userId: string;
  role: ParticipantRole;
  displayName: string;
  avatarUrl: string;
};

export type SearchFilters = {
  conversationId?: string;
  senderId?: string;
  from?: string;
  to?: string;
  hasAttachments?: boolean;
  attachmentType?: AttachmentType;
};

// Gateway матчит /api/chat/* и снимает этот префикс при проксировании
// в chat-service (см. src/Gateway/ApiGateway/appsettings.json). Префикс
// /api/v1 на уровне самого сервиса — задача Gateway (issue #215),
// а не фронта. Единый паттерн со всеми остальными сервисами (users/media/...).
const PREFIX = "/api/chat";

export function createChatApi(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  const request = createRequest(baseUrl, authHeaders, handleUnauthorized);

  return {
    getConversations(type?: string, cursor?: string, limit?: number): Promise<Paginated<ConversationPreview>> {
      const params = new URLSearchParams();
      if (type) params.append("type", type);
      if (cursor) params.append("cursor", cursor);
      if (limit) params.append("limit", limit.toString());
      return request<Paginated<ConversationPreview>>(`${PREFIX}/conversations?${params.toString()}`);
    },

    openDirectConversation(peerUserId: string): Promise<ConversationPreview> {
      return request<ConversationPreview>(`${PREFIX}/conversations/direct`, {
        method: "POST",
        body: JSON.stringify({ peerUserId }),
      });
    },

    getConversation(id: string): Promise<ConversationPreview> {
      return request<ConversationPreview>(`${PREFIX}/conversations/${encodeURIComponent(id)}`);
    },

    getPinnedMessages(id: string): Promise<MessageDto[]> {
      return request<MessageDto[]>(`${PREFIX}/conversations/${encodeURIComponent(id)}/pinned`, {
        method: "GET",
      });
    },

    getConversationParticipants(id: string): Promise<ConversationParticipantDto[]> {
      return request<ConversationParticipantDto[]>(
        `${PREFIX}/conversations/${encodeURIComponent(id)}/participants`,
      );
    },

    getConversationMessages(id: string, cursor?: string, limit?: number, direction?: "older" | "newer"): Promise<Paginated<MessageDto>> {
      const params = new URLSearchParams();
      if (cursor) params.append("cursor", cursor);
      if (limit) params.append("limit", limit.toString());
      if (direction) params.append("direction", direction);
      return request<Paginated<MessageDto>>(`${PREFIX}/conversations/${encodeURIComponent(id)}/messages?${params.toString()}`);
    },

    searchMessages(
      query: string,
      conversationId?: string,
      cursor?: string,
      limit?: number,
      filters?: SearchFilters,
      signal?: AbortSignal,
    ): Promise<Paginated<SearchResultDto>> {
      const params = new URLSearchParams();
      params.append("q", query);
      if (conversationId) params.append("conversationId", conversationId);
      if (cursor) params.append("cursor", cursor);
      if (limit) params.append("limit", limit.toString());
      if (filters?.senderId) params.append("senderId", filters.senderId);
      if (filters?.from) params.append("from", filters.from);
      if (filters?.to) params.append("to", filters.to);
      if (typeof filters?.hasAttachments === "boolean") params.append("hasAttachments", String(filters.hasAttachments));
      if (filters?.attachmentType) params.append("attachmentType", filters.attachmentType);
      return request<Paginated<SearchResultDto>>(`${PREFIX}/search?${params.toString()}`, { signal });
    },

    editMessage(messageId: string, body: string): Promise<MessageDto> {
      return request<MessageDto>(`${PREFIX}/messages/${encodeURIComponent(messageId)}`, {
        method: "PATCH",
        body: JSON.stringify({ body }),
      });
    },

    deleteMessage(messageId: string, mode: DeleteMode = "for-me"): Promise<MessageDto | null> {
      return request<MessageDto | null>(`${PREFIX}/messages/${encodeURIComponent(messageId)}?mode=${mode}`, {
        method: "DELETE",
      });
    },

    forwardMessages(targetConversationId: string, messageIds: string[]): Promise<MessageDto[]> {
      return request<MessageDto[]>(`${PREFIX}/conversations/${encodeURIComponent(targetConversationId)}/forward`, {
        method: "POST",
        body: JSON.stringify({ messageIds }),
      });
    },

    addReaction(messageId: string, emoji: string): Promise<void> {
      return request<void>(`${PREFIX}/messages/${encodeURIComponent(messageId)}/reactions`, {
        method: "POST",
        body: JSON.stringify({ emoji }),
      });
    },

    removeReaction(messageId: string, emoji: string): Promise<void> {
      return request<void>(`${PREFIX}/messages/${encodeURIComponent(messageId)}/reactions/${encodeURIComponent(emoji)}`, {
        method: "DELETE",
      });
    },

    pinMessage(conversationId: string, messageId: string): Promise<MessageDto[]> {
      return request<MessageDto[]>(`${PREFIX}/conversations/${encodeURIComponent(conversationId)}/pinned`, {
        method: "POST",
        body: JSON.stringify({ messageId }),
      });
    },

    unpinMessage(conversationId: string, messageId: string): Promise<MessageDto[]> {
      return request<MessageDto[]>(`${PREFIX}/conversations/${encodeURIComponent(conversationId)}/pinned/${encodeURIComponent(messageId)}`, {
        method: "DELETE",
      });
    },

    getReadReceipts(messageId: string): Promise<ReadReceiptDto[]> {
      return request<ReadReceiptDto[]>(`${PREFIX}/messages/${encodeURIComponent(messageId)}/read-receipts`);
    },

    getThreadMessages(rootMessageId: string, cursor?: string, limit?: number, direction?: "older" | "newer"): Promise<Paginated<MessageDto>> {
      const params = new URLSearchParams();
      if (cursor) params.append("cursor", cursor);
      if (limit) params.append("limit", limit.toString());
      if (direction) params.append("direction", direction);
      return request<Paginated<MessageDto>>(`${PREFIX}/messages/${encodeURIComponent(rootMessageId)}/thread?${params.toString()}`);
    },

    replyInThread(rootMessageId: string, body: string, attachmentAssetIds: string[] = [], replyToMessageId?: string, clientMessageId?: string): Promise<MessageDto> {
      return request<MessageDto>(`${PREFIX}/messages/${encodeURIComponent(rootMessageId)}/thread`, {
        method: "POST",
        body: JSON.stringify({
          body,
          attachmentAssetIds,
          replyToMessageId,
          clientMessageId: clientMessageId ?? crypto.randomUUID(),
        }),
      });
    },

    subscribeToThread(rootMessageId: string): Promise<void> {
      return request<void>(`${PREFIX}/messages/${encodeURIComponent(rootMessageId)}/thread/subscribe`, {
        method: "POST",
      });
    },

    unsubscribeFromThread(rootMessageId: string): Promise<void> {
      return request<void>(`${PREFIX}/messages/${encodeURIComponent(rootMessageId)}/thread/subscribe`, {
        method: "DELETE",
      });
    },

    getActiveThreads(cursor?: string, limit?: number): Promise<Paginated<ActiveThreadDto>> {
      const params = new URLSearchParams();
      if (cursor) params.append("cursor", cursor);
      if (limit) params.append("limit", limit.toString());
      return request<Paginated<ActiveThreadDto>>(`${PREFIX}/threads/active?${params.toString()}`);
    },
  };
}
