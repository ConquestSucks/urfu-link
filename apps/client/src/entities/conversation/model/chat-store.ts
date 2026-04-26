import { create } from "zustand";
import { ConversationPreview, MessageDto } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";
import { apiClient } from "@/shared/lib/api";
import { useAuthStore } from "@/shared/store/auth-store";

export type ChatMessagePropsMapped = {
    id: string;
    text: string;
    isOwn: boolean;
    time: string;
    avatarUrl: string;
    showAvatar?: boolean;
    seen?: boolean;
    attachments?: { name: string; url: string }[];
};

type ChatState = {
    connection: HubConnection | null;
    isConnected: boolean;
    conversations: ConversationPreview[];
    messagesByConversation: Record<string, ChatMessagePropsMapped[]>;
    cursors: Record<string, string | undefined>;
    hasMoreByConversation: Record<string, boolean>;
    isLoading: boolean;
    
    connect: () => Promise<void>;
    disconnect: () => Promise<void>;
    
    loadConversations: (type?: "Direct" | "Discipline") => Promise<void>;
    loadMessages: (chatId: string, type: "chat" | "subject", reset?: boolean) => Promise<void>;
    loadMore: (chatId: string, type: "chat" | "subject") => Promise<void>;
    sendMessage: (chatId: string, text: string, attachments?: string[]) => Promise<void>;
    markRead: (chatId: string, messageId: string) => Promise<void>;
    
    addMessage: (message: MessageDto) => void;
    updateConversation: (conversation: ConversationPreview) => void;
};

const mapMessage = (dto: MessageDto, currentUserId?: string | null): ChatMessagePropsMapped => {
    const timeStr = new Date(dto.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    return {
        id: dto.id,
        text: dto.body,
        isOwn: dto.senderId === currentUserId,
        time: timeStr,
        avatarUrl: "", // Need to resolve from user info
        seen: dto.readAt !== null,
        attachments: dto.attachments.map(a => ({
            name: a.fileName,
            // Temporary, should use mediaApi to get download url
            url: `/api/media/${a.mediaAssetId}/download-url`
        }))
    };
};

export const useChatStore = create<ChatState>((set, get) => ({
    connection: null,
    isConnected: false,
    conversations: [],
    messagesByConversation: {},
    cursors: {},
    hasMoreByConversation: {},
    isLoading: false,

    connect: async () => {
        const { connection, isConnected } = get();
        if (isConnected || connection?.state === HubConnectionState.Connected) {
            return;
        }

        const newConnection = createHubConnection("/hubs/chat");

        newConnection.on("MessageReceived", (message: MessageDto) => {
            get().addMessage(message);
        });

        newConnection.on("ConversationUpdated", (conversation: ConversationPreview) => {
            get().updateConversation(conversation);
        });

        newConnection.on("MessageReadUpdate", (conversationId: string, upToMessageId: string, readerUserId: string) => {
            set((state) => {
                const msgs = state.messagesByConversation[conversationId];
                if (!msgs) return state;

                // Find the index of the message we read up to.
                // Assuming newer messages are at the beginning (index 0) due to inverted FlatList.
                const readIndex = msgs.findIndex(m => m.id === upToMessageId);
                if (readIndex === -1) return state;

                // Mark all messages from readIndex to the end (older messages) as seen.
                const updatedMsgs = msgs.map((m, idx) => {
                    if (idx >= readIndex && !m.seen) {
                        return { ...m, seen: true };
                    }
                    return m;
                });

                return {
                    messagesByConversation: {
                        ...state.messagesByConversation,
                        [conversationId]: updatedMsgs
                    }
                };
            });
        });

        try {
            await newConnection.start();
            set({ connection: newConnection, isConnected: true });
        } catch (e) {
            console.error("Failed to connect to ChatHub", e);
            set({ isConnected: false });
        }
    },

    disconnect: async () => {
        const { connection } = get();
        if (connection) {
            await connection.stop();
            set({ connection: null, isConnected: false });
        }
    },

    loadConversations: async (type) => {
        try {
            const res = await apiClient.chat.getConversations(type, undefined, 50);
            set({ conversations: res.items });
        } catch (error) {
            console.error("Failed to load conversations", error);
        }
    },

    loadMessages: async (chatId, type, reset = false) => {
        const state = get();
        const currentHasMore = state.hasMoreByConversation[chatId] ?? true;
        
        if (!reset && !currentHasMore) return;
        
        set({ isLoading: true });
        try {
            const cursor = reset ? undefined : state.cursors[chatId];
            const res = await apiClient.chat.getConversationMessages(chatId, cursor, 20, "older");
            
            const currentUserId = useAuthStore.getState().accessToken ? "me" : null; // Hack: need real user id
            const mapped = res.items.map(m => mapMessage(m, currentUserId));
            
            set((prev) => ({
                messagesByConversation: {
                    ...prev.messagesByConversation,
                    [chatId]: reset ? mapped : [...(prev.messagesByConversation[chatId] || []), ...mapped]
                },
                cursors: {
                    ...prev.cursors,
                    [chatId]: res.nextCursor
                },
                hasMoreByConversation: {
                    ...prev.hasMoreByConversation,
                    [chatId]: !!res.nextCursor
                },
                isLoading: false
            }));
        } catch (error) {
            console.error("Failed to load messages", error);
            set({ isLoading: false });
        }
    },

    loadMore: async (chatId, type) => {
        const { isLoading, hasMoreByConversation, loadMessages } = get();
        if (isLoading || !hasMoreByConversation[chatId]) return;
        await loadMessages(chatId, type, false);
    },

    sendMessage: async (chatId, text, attachments = []) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            const clientMessageId = crypto.randomUUID();
            await connection.invoke("SendMessage", chatId, text, attachments, clientMessageId);
        }
    },

    markRead: async (chatId, messageId) => {
        const { connection } = get();
        if (connection?.state === HubConnectionState.Connected) {
            await connection.invoke("MarkRead", chatId, messageId);
        }
    },

    addMessage: (message) => {
        set((state) => {
            const conversationId = message.conversationId;
            const existing = state.messagesByConversation[conversationId] || [];
            
            if (existing.some(m => m.id === message.id)) {
                return state;
            }
            
            const currentUserId = useAuthStore.getState().accessToken ? "me" : null; // need real UI
            const mapped = mapMessage(message, currentUserId);

            return {
                messagesByConversation: {
                    ...state.messagesByConversation,
                    // Prepend because FlatList inverted=true expects newest at start
                    [conversationId]: [mapped, ...existing]
                }
            };
        });
    },

    updateConversation: (conversation) => {
        set((state) => {
            const index = state.conversations.findIndex(c => c.id === conversation.id);
            if (index === -1) {
                return { conversations: [conversation, ...state.conversations] };
            }

            const newConversations = [...state.conversations];
            newConversations[index] = conversation;
            
            newConversations.sort((a, b) => {
                const dateA = a.lastMessageAt ? new Date(a.lastMessageAt).getTime() : 0;
                const dateB = b.lastMessageAt ? new Date(b.lastMessageAt).getTime() : 0;
                return dateB - dateA;
            });

            return { conversations: newConversations };
        });
    }
}));
