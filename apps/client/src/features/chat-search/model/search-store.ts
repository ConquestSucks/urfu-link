import { create } from "zustand";
import { SearchResultDto } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";

type SearchState = {
    // Global search (all conversations)
    globalQuery: string;
    globalResults: SearchResultDto[];
    isGlobalLoading: boolean;
    globalNextCursor?: string;

    // Local search (within a conversation)
    localQuery: string;
    localResults: SearchResultDto[];
    isLocalLoading: boolean;
    localConversationId: string | null;
    localNextCursor?: string;

    setGlobalQuery: (query: string) => void;
    searchGlobal: (query: string) => Promise<void>;
    loadMoreGlobal: () => Promise<void>;

    setLocalQuery: (query: string) => void;
    searchLocal: (conversationId: string, query: string) => Promise<void>;
    loadMoreLocal: () => Promise<void>;
    clearLocal: () => void;
};

const MIN_QUERY_LENGTH = 2;

export const useSearchStore = create<SearchState>((set, get) => ({
    globalQuery: "",
    globalResults: [],
    isGlobalLoading: false,
    globalNextCursor: undefined,

    localQuery: "",
    localResults: [],
    isLocalLoading: false,
    localConversationId: null,
    localNextCursor: undefined,

    setGlobalQuery: (query) => set({ globalQuery: query }),

    searchGlobal: async (query) => {
        if (query.length < MIN_QUERY_LENGTH) {
            set({ globalResults: [], globalNextCursor: undefined });
            return;
        }
        set({ isGlobalLoading: true, globalQuery: query });
        try {
            const res = await apiClient.chat.searchMessages(query, undefined, undefined, 20);
            set({
                globalResults: res.items,
                globalNextCursor: res.nextCursor,
                isGlobalLoading: false,
            });
        } catch (error) {
            console.error("Global search failed", error);
            set({ isGlobalLoading: false });
        }
    },

    loadMoreGlobal: async () => {
        const { globalQuery, globalNextCursor, isGlobalLoading, globalResults } = get();
        if (isGlobalLoading || !globalNextCursor) return;
        set({ isGlobalLoading: true });
        try {
            const res = await apiClient.chat.searchMessages(globalQuery, undefined, globalNextCursor, 20);
            set({
                globalResults: [...globalResults, ...res.items],
                globalNextCursor: res.nextCursor,
                isGlobalLoading: false,
            });
        } catch (error) {
            console.error("Load more global failed", error);
            set({ isGlobalLoading: false });
        }
    },

    setLocalQuery: (query) => set({ localQuery: query }),

    searchLocal: async (conversationId, query) => {
        if (query.length < MIN_QUERY_LENGTH) {
            set({ localResults: [], localNextCursor: undefined });
            return;
        }
        set({ isLocalLoading: true, localQuery: query, localConversationId: conversationId });
        try {
            const res = await apiClient.chat.searchMessages(query, conversationId, undefined, 20);
            set({
                localResults: res.items,
                localNextCursor: res.nextCursor,
                isLocalLoading: false,
            });
        } catch (error) {
            console.error("Local search failed", error);
            set({ isLocalLoading: false });
        }
    },

    loadMoreLocal: async () => {
        const { localQuery, localConversationId, localNextCursor, isLocalLoading, localResults } = get();
        if (isLocalLoading || !localNextCursor || !localConversationId) return;
        set({ isLocalLoading: true });
        try {
            const res = await apiClient.chat.searchMessages(localQuery, localConversationId, localNextCursor, 20);
            set({
                localResults: [...localResults, ...res.items],
                localNextCursor: res.nextCursor,
                isLocalLoading: false,
            });
        } catch (error) {
            console.error("Load more local failed", error);
            set({ isLocalLoading: false });
        }
    },

    clearLocal: () => set({
        localQuery: "",
        localResults: [],
        localNextCursor: undefined,
        localConversationId: null,
        isLocalLoading: false,
    }),
}));
