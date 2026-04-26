import { AuthHeaders, HandleUnauthorized, createRequest } from "./utils";

export type ConversationType = "Direct" | "Discipline";

export type ConversationPreview = {
  id: string;
  type: ConversationType;
  participants: string[];
  createdAt: string;
  lastMessageAt: string | null;
  lastMessagePreview: {
    senderId: string;
    body: string;
    sentAt: string;
  } | null;
};

export type MessageState = "Sent" | "Delivered" | "Read" | "Deleted";
export type AttachmentType = "Image" | "Video" | "Audio" | "Voice" | "Document";

export type ChatAttachment = {
  mediaAssetId: string;
  type: AttachmentType;
  thumbnailAssetId?: string;
  fileName: string;
  size: number;
  mimeType: string;
};

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
};

export type Paginated<T> = {
  items: T[];
  nextCursor?: string;
};

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
      return request<Paginated<ConversationPreview>>(`/api/chat/conversations?${params.toString()}`);
    },

    openDirectConversation(peerUserId: string): Promise<ConversationPreview> {
      return request<ConversationPreview>("/api/chat/conversations/direct", {
        method: "POST",
        body: JSON.stringify({ peerUserId }),
      });
    },

    getConversation(id: string): Promise<ConversationPreview> {
      return request<ConversationPreview>(`/api/chat/conversations/${encodeURIComponent(id)}`);
    },

    getConversationMessages(id: string, cursor?: string, limit?: number, direction?: "older" | "newer"): Promise<Paginated<MessageDto>> {
      const params = new URLSearchParams();
      if (cursor) params.append("cursor", cursor);
      if (limit) params.append("limit", limit.toString());
      if (direction) params.append("direction", direction);
      return request<Paginated<MessageDto>>(`/api/chat/conversations/${encodeURIComponent(id)}/messages?${params.toString()}`);
    },

    searchMessages(query: string, conversationId?: string, cursor?: string, limit?: number): Promise<Paginated<any>> {
      const params = new URLSearchParams();
      params.append("q", query);
      if (conversationId) params.append("conversationId", conversationId);
      if (cursor) params.append("cursor", cursor);
      if (limit) params.append("limit", limit.toString());
      return request<Paginated<any>>(`/api/chat/search?${params.toString()}`);
    }
  };
}
