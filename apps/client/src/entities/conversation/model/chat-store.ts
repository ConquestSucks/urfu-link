import { create } from "zustand";
import { ConversationPreview, MessageDto } from "@urfu-link/api-client";
import { createHubConnection } from "@/shared/lib/signalr";
import { HubConnection, HubConnectionState } from "@microsoft/signalr";

type ChatState = {
    connection: HubConnection | null;
    isConnected: boolean;
    conversations: ConversationPreview[];
    messagesByConversation: Record<string, MessageDto[]>;
    
    connect: () => Promise<void>;
    disconnect: () => Promise<void>;
    
    setConversations: (conversations: ConversationPreview[]) => void;
    addMessage: (message: MessageDto) => void;
    updateConversation: (conversation: ConversationPreview) => void;
};

export const useChatStore = create<ChatState>((set, get) => ({
    connection: null,
    isConnected: false,
    conversations: [],
    messagesByConversation: {},

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

    setConversations: (conversations) => {
        set({ conversations });
    },

    addMessage: (message) => {
        set((state) => {
            const conversationId = message.conversationId;
            const existing = state.messagesByConversation[conversationId] || [];
            
            // Prevent duplicates
            if (existing.some(m => m.id === message.id)) {
                return state;
            }

            return {
                messagesByConversation: {
                    ...state.messagesByConversation,
                    [conversationId]: [...existing, message]
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
            
            // Sort by lastMessageAt descending
            newConversations.sort((a, b) => {
                const dateA = a.lastMessageAt ? new Date(a.lastMessageAt).getTime() : 0;
                const dateB = b.lastMessageAt ? new Date(b.lastMessageAt).getTime() : 0;
                return dateB - dateA;
            });

            return { conversations: newConversations };
        });
    }
}));
