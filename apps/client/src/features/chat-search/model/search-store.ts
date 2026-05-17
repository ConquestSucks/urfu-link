import { create } from "zustand";
import { SearchResultDto } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";

type SearchState = {
    // Global search (all conversations)
    globalQuery: string;
    globalResults: SearchResultDto[];
    isGlobalLoading: boolean;
    globalError: string | null;
    globalNextCursor?: string;

    // Local search (within a conversation)
    localQuery: string;
    localResults: SearchResultDto[];
    isLocalLoading: boolean;
    localError: string | null;
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

const isAbortError = (error: unknown): boolean =>
    error instanceof Error && error.name === "AbortError";

const messageFromError = (error: unknown): string =>
    error instanceof Error ? error.message : "Не удалось выполнить поиск";

const MIN_QUERY_LENGTH = 2;

export const useSearchStore = create<SearchState>((set, get) => ({
    globalQuery: "",
    globalResults: [],
    isGlobalLoading: false,
    globalError: null,
    globalNextCursor: undefined,

    localQuery: "",
    localResults: [],
    isLocalLoading: false,
    localError: null,
    localConversationId: null,
    localNextCursor: undefined,

    setGlobalQuery: (query) => set({ globalQuery: query }),

    searchGlobal: async (query) => {
        if (query.length < MIN_QUERY_LENGTH) {
            set({ globalResults: [], globalNextCursor: undefined, globalError: null });
            return;
        }
        set({ isGlobalLoading: true, globalQuery: query, globalError: null });
        try {
            const res = await apiClient.chat.searchMessages(query, undefined, undefined, 20);
            set({
                globalResults: res.items,
                globalNextCursor: res.nextCursor,
                isGlobalLoading: false,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Global search failed", error);
            set({ isGlobalLoading: false, globalError: messageFromError(error) });
        }
    },

    loadMoreGlobal: async () => {
        const { globalQuery, globalNextCursor, isGlobalLoading, globalResults } = get();
        if (isGlobalLoading || !globalNextCursor) return;
        set({ isGlobalLoading: true, globalError: null });
        try {
            const res = await apiClient.chat.searchMessages(globalQuery, undefined, globalNextCursor, 20);
            set({
                globalResults: [...globalResults, ...res.items],
                globalNextCursor: res.nextCursor,
                isGlobalLoading: false,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Load more global failed", error);
            set({ isGlobalLoading: false, globalError: messageFromError(error) });
        }
    },

    setLocalQuery: (query) => set({ localQuery: query }),

    searchLocal: async (conversationId, query) => {
        if (query.length < MIN_QUERY_LENGTH) {
            set({ localResults: [], localNextCursor: undefined, localError: null });
            return;
        }
        set({
            isLocalLoading: true,
            localQuery: query,
            localConversationId: conversationId,
            localError: null,
        });
        try {
            const res = await apiClient.chat.searchMessages(query, conversationId, undefined, 20);
            set({
                localResults: res.items,
                localNextCursor: res.nextCursor,
                isLocalLoading: false,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Local search failed", error);
            set({ isLocalLoading: false, localError: messageFromError(error) });
        }
    },

    loadMoreLocal: async () => {
        const { localQuery, localConversationId, localNextCursor, isLocalLoading, localResults } = get();
        if (isLocalLoading || !localNextCursor || !localConversationId) return;
        set({ isLocalLoading: true, localError: null });
        try {
            const res = await apiClient.chat.searchMessages(localQuery, localConversationId, localNextCursor, 20);
            set({
                localResults: [...localResults, ...res.items],
                localNextCursor: res.nextCursor,
                isLocalLoading: false,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Load more local failed", error);
            set({ isLocalLoading: false, localError: messageFromError(error) });
        }
    },

    clearLocal: () => set({
        localQuery: "",
        localResults: [],
        localNextCursor: undefined,
        localConversationId: null,
        isLocalLoading: false,
        localError: null,
    }),
}));
