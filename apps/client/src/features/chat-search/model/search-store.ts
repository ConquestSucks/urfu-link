import { create } from "zustand";
import { SearchFilters, SearchResultDto } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";

// Поля, которыми UI рулит из SearchFiltersBar. conversationId исключён —
// он задаётся самим режимом поиска (global/local), а не пользователем.
export type SearchFilterValues = Omit<SearchFilters, "conversationId">;

type SearchState = {
    // Global search (all conversations)
    globalQuery: string;
    globalResults: SearchResultDto[];
    isGlobalLoading: boolean;
    globalError: string | null;
    globalNextCursor?: string;
    globalAbort: AbortController | null;
    globalFilters: SearchFilterValues;

    // Local search (within a conversation)
    localQuery: string;
    localResults: SearchResultDto[];
    isLocalLoading: boolean;
    localError: string | null;
    localConversationId: string | null;
    localNextCursor?: string;
    localAbort: AbortController | null;
    localFilters: SearchFilterValues;

    setGlobalQuery: (query: string) => void;
    setGlobalFilters: (filters: SearchFilterValues) => void;
    searchGlobal: (query: string) => Promise<void>;
    loadMoreGlobal: () => Promise<void>;

    setLocalQuery: (query: string) => void;
    setLocalFilters: (filters: SearchFilterValues) => void;
    searchLocal: (conversationId: string, query: string) => Promise<void>;
    loadMoreLocal: () => Promise<void>;
    clearLocal: () => void;
};

const isAbortError = (error: unknown): boolean =>
    (error instanceof Error && (error.name === "AbortError" || error.name === "CanceledError")) ||
    // fetch в RN иногда выбрасывает DOMException-like объект без instanceof Error
    (typeof error === "object" && error !== null && (error as { name?: string }).name === "AbortError");

const messageFromError = (error: unknown): string =>
    error instanceof Error ? error.message : "Не удалось выполнить поиск";

const MIN_QUERY_LENGTH = 2;

const EMPTY_FILTERS: SearchFilterValues = {};

const filtersToApi = (filters: SearchFilterValues): SearchFilters | undefined => {
    const hasAny =
        filters.senderId ||
        filters.from ||
        filters.to ||
        typeof filters.hasAttachments === "boolean" ||
        filters.attachmentType;
    return hasAny ? filters : undefined;
};

export const useSearchStore = create<SearchState>((set, get) => ({
    globalQuery: "",
    globalResults: [],
    isGlobalLoading: false,
    globalError: null,
    globalNextCursor: undefined,
    globalAbort: null,
    globalFilters: EMPTY_FILTERS,

    localQuery: "",
    localResults: [],
    isLocalLoading: false,
    localError: null,
    localConversationId: null,
    localNextCursor: undefined,
    localAbort: null,
    localFilters: EMPTY_FILTERS,

    setGlobalQuery: (query) => set({ globalQuery: query }),

    setGlobalFilters: (filters) => {
        set({ globalFilters: filters });
        // Изменение фильтра — это запрос с другим срезом результатов: перезапускаем
        // поиск, если query валидный, иначе просто чистим выдачу.
        const { globalQuery } = get();
        if (globalQuery.length >= MIN_QUERY_LENGTH) {
            void get().searchGlobal(globalQuery);
        } else {
            set({ globalResults: [], globalNextCursor: undefined });
        }
    },

    searchGlobal: async (query) => {
        // Отменяем in-flight запрос предыдущего поиска.
        get().globalAbort?.abort();

        if (query.length < MIN_QUERY_LENGTH) {
            set({ globalResults: [], globalNextCursor: undefined, globalError: null, globalAbort: null });
            return;
        }

        const controller = new AbortController();
        set({ isGlobalLoading: true, globalQuery: query, globalError: null, globalAbort: controller });
        try {
            const res = await apiClient.chat.searchMessages(
                query,
                undefined,
                undefined,
                20,
                filtersToApi(get().globalFilters),
                controller.signal,
            );
            // Если за время запроса нас отменили — не пишем результаты в state.
            if (controller.signal.aborted) return;
            set({
                globalResults: res.items,
                globalNextCursor: res.nextCursor,
                isGlobalLoading: false,
                globalAbort: null,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Global search failed", error);
            set({ isGlobalLoading: false, globalError: messageFromError(error), globalAbort: null });
        }
    },

    loadMoreGlobal: async () => {
        const { globalQuery, globalNextCursor, isGlobalLoading, globalResults } = get();
        if (isGlobalLoading || !globalNextCursor) return;
        const controller = new AbortController();
        set({ isGlobalLoading: true, globalError: null, globalAbort: controller });
        try {
            const res = await apiClient.chat.searchMessages(
                globalQuery,
                undefined,
                globalNextCursor,
                20,
                filtersToApi(get().globalFilters),
                controller.signal,
            );
            if (controller.signal.aborted) return;
            set({
                globalResults: [...globalResults, ...res.items],
                globalNextCursor: res.nextCursor,
                isGlobalLoading: false,
                globalAbort: null,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Load more global failed", error);
            set({ isGlobalLoading: false, globalError: messageFromError(error), globalAbort: null });
        }
    },

    setLocalQuery: (query) => set({ localQuery: query }),

    setLocalFilters: (filters) => {
        set({ localFilters: filters });
        const { localQuery, localConversationId } = get();
        if (localConversationId && localQuery.length >= MIN_QUERY_LENGTH) {
            void get().searchLocal(localConversationId, localQuery);
        } else {
            set({ localResults: [], localNextCursor: undefined });
        }
    },

    searchLocal: async (conversationId, query) => {
        get().localAbort?.abort();

        if (query.length < MIN_QUERY_LENGTH) {
            set({ localResults: [], localNextCursor: undefined, localError: null, localAbort: null });
            return;
        }

        const controller = new AbortController();
        set({
            isLocalLoading: true,
            localQuery: query,
            localConversationId: conversationId,
            localError: null,
            localAbort: controller,
        });
        try {
            const res = await apiClient.chat.searchMessages(
                query,
                conversationId,
                undefined,
                20,
                filtersToApi(get().localFilters),
                controller.signal,
            );
            if (controller.signal.aborted) return;
            // Дополнительная защита от race: пока запрос летел, юзер мог переключить чат.
            if (get().localConversationId !== conversationId) return;
            set({
                localResults: res.items,
                localNextCursor: res.nextCursor,
                isLocalLoading: false,
                localAbort: null,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Local search failed", error);
            set({ isLocalLoading: false, localError: messageFromError(error), localAbort: null });
        }
    },

    loadMoreLocal: async () => {
        const { localQuery, localConversationId, localNextCursor, isLocalLoading, localResults } = get();
        if (isLocalLoading || !localNextCursor || !localConversationId) return;
        const controller = new AbortController();
        set({ isLocalLoading: true, localError: null, localAbort: controller });
        try {
            const res = await apiClient.chat.searchMessages(
                localQuery,
                localConversationId,
                localNextCursor,
                20,
                filtersToApi(get().localFilters),
                controller.signal,
            );
            if (controller.signal.aborted) return;
            if (get().localConversationId !== localConversationId) return;
            set({
                localResults: [...localResults, ...res.items],
                localNextCursor: res.nextCursor,
                isLocalLoading: false,
                localAbort: null,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Load more local failed", error);
            set({ isLocalLoading: false, localError: messageFromError(error), localAbort: null });
        }
    },

    clearLocal: () => {
        get().localAbort?.abort();
        set({
            localQuery: "",
            localResults: [],
            localNextCursor: undefined,
            localConversationId: null,
            isLocalLoading: false,
            localError: null,
            localAbort: null,
            localFilters: EMPTY_FILTERS,
        });
    },
}));
