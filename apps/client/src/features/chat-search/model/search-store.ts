import { create } from "zustand";
import { SearchFilters, SearchResultDto, SearchUserDto } from "@urfu-link/api-client";
import { apiClient } from "@/shared/lib/api";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useParticipantsStore } from "@/entities/conversation/model/participants-store";

// Поля, которыми UI рулит из SearchFiltersBar. conversationId исключён —
// он задаётся самим режимом поиска (global/local), а не пользователем.
export type SearchFilterValues = Omit<SearchFilters, "conversationId" | "senderId">;

// Глобальный поиск имеет два scope: сообщения (messages) и пользователи (users).
// Один SearchBar управляет обоими — scope переключается табами в GlobalSearchPanel.
export type GlobalSearchScope = "messages" | "users";

type SearchState = {
    // Global search (all conversations)
    globalScope: GlobalSearchScope;
    globalQuery: string;
    globalResults: SearchResultDto[];
    isGlobalLoading: boolean;
    globalError: string | null;
    globalNextCursor?: string;
    globalAbort: AbortController | null;
    globalFilters: SearchFilterValues;

    // Global user search (отдельная ветка state — независимые результаты и pagination)
    userResults: SearchUserDto[];
    isUsersLoading: boolean;
    usersError: string | null;
    usersNextOffset?: number;
    usersAbort: AbortController | null;
    // Идентификатор пользователя, для которого сейчас открывается direct-чат.
    // Блокирует повторный tap по той же строке и показывает spinner справа.
    pendingUserId: string | null;

    // Local search (within a conversation)
    localQuery: string;
    localResults: SearchResultDto[];
    isLocalLoading: boolean;
    localError: string | null;
    localConversationId: string | null;
    localNextCursor?: string;
    localAbort: AbortController | null;
    localFilters: SearchFilterValues;

    setGlobalScope: (scope: GlobalSearchScope) => void;
    setGlobalQuery: (query: string) => void;
    setGlobalFilters: (filters: SearchFilterValues) => void;
    searchGlobal: (query: string) => Promise<void>;
    loadMoreGlobal: () => Promise<void>;

    searchUsers: (query: string) => Promise<void>;
    loadMoreUsers: () => Promise<void>;
    // Возвращает conversationId direct-чата (либо ловит ошибку и возвращает null).
    // Бэкенд идемпотентен по peerUserId — повторный вызов вернёт тот же чат.
    openDirectWithUser: (user: SearchUserDto) => Promise<string | null>;
    clearGlobal: () => void;

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
const USERS_PAGE_SIZE = 20;

const EMPTY_FILTERS: SearchFilterValues = {};

const filtersToApi = (filters: SearchFilterValues): SearchFilters | undefined => {
    const hasAny =
        filters.from ||
        filters.to ||
        typeof filters.hasAttachments === "boolean" ||
        filters.attachmentType;
    return hasAny ? filters : undefined;
};

export const useSearchStore = create<SearchState>((set, get) => ({
    globalScope: "messages",
    globalQuery: "",
    globalResults: [],
    isGlobalLoading: false,
    globalError: null,
    globalNextCursor: undefined,
    globalAbort: null,
    globalFilters: EMPTY_FILTERS,

    userResults: [],
    isUsersLoading: false,
    usersError: null,
    usersNextOffset: undefined,
    usersAbort: null,
    pendingUserId: null,

    localQuery: "",
    localResults: [],
    isLocalLoading: false,
    localError: null,
    localConversationId: null,
    localNextCursor: undefined,
    localAbort: null,
    localFilters: EMPTY_FILTERS,

    setGlobalScope: (scope) => {
        if (get().globalScope === scope) return;
        set({ globalScope: scope });
        // Смена scope — запускаем поиск в новом режиме, если query валидный.
        // Отменяем in-flight запросы предыдущего scope, чтобы их результаты не
        // прилетели поверх новых.
        const { globalQuery } = get();
        if (globalQuery.length >= MIN_QUERY_LENGTH) {
            if (scope === "users") {
                get().globalAbort?.abort();
                void get().searchUsers(globalQuery);
            } else {
                get().usersAbort?.abort();
                void get().searchGlobal(globalQuery);
            }
        }
    },

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

    searchUsers: async (query) => {
        get().usersAbort?.abort();

        if (query.length < MIN_QUERY_LENGTH) {
            set({ userResults: [], usersNextOffset: undefined, usersError: null, usersAbort: null });
            return;
        }

        const controller = new AbortController();
        set({
            isUsersLoading: true,
            globalQuery: query,
            usersError: null,
            usersAbort: controller,
        });
        try {
            const res = await apiClient.users.searchUsers(
                query,
                0,
                USERS_PAGE_SIZE,
                controller.signal,
            );
            if (controller.signal.aborted) return;
            set({
                userResults: res.items,
                usersNextOffset: res.hasMore ? res.items.length : undefined,
                isUsersLoading: false,
                usersAbort: null,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("User search failed", error);
            set({ isUsersLoading: false, usersError: messageFromError(error), usersAbort: null });
        }
    },

    loadMoreUsers: async () => {
        const { globalQuery, usersNextOffset, isUsersLoading, userResults } = get();
        if (isUsersLoading || usersNextOffset === undefined) return;
        const controller = new AbortController();
        set({ isUsersLoading: true, usersError: null, usersAbort: controller });
        try {
            const res = await apiClient.users.searchUsers(
                globalQuery,
                usersNextOffset,
                USERS_PAGE_SIZE,
                controller.signal,
            );
            if (controller.signal.aborted) return;
            const combined = [...userResults, ...res.items];
            set({
                userResults: combined,
                usersNextOffset: res.hasMore ? combined.length : undefined,
                isUsersLoading: false,
                usersAbort: null,
            });
        } catch (error) {
            if (isAbortError(error)) return;
            console.error("Load more users failed", error);
            set({ isUsersLoading: false, usersError: messageFromError(error), usersAbort: null });
        }
    },

    openDirectWithUser: async (user) => {
        const userId = user.id;
        // Если по этому id уже идёт запрос — не дубль. Если по другому — разрешаем
        // (race-protection: бэк идемпотентен, оба запроса вернут один и тот же чат).
        if (get().pendingUserId === userId) return null;
        set({ pendingUserId: userId });
        try {
            const conversation = await apiClient.chat.openDirectConversation(userId);

            // Локально регистрируем чат в chat-store сразу, не дожидаясь SignalR
            // ConversationUpdated — иначе ChatHeader/Inbox не найдут conversation
            // по id после router.push. Если SignalR-событие потом прилетит, оно
            // просто перезапишет ту же запись (updateConversation идемпотентен).
            useChatStore.getState().updateConversation(conversation);
            useParticipantsStore.getState().prime(conversation.id, [
                {
                    userId,
                    role: "Member",
                    displayName: user.displayName || user.username,
                    avatarUrl: user.avatarUrl ?? "",
                },
            ]);

            // Для нового direct-чата backend возвращает draft без Mongo-документа, поэтому
            // /participants до первого сообщения ещё недоступен. Имя собеседника уже есть из
            // поиска и primed выше; persisted-чаты можно дозагрузить обычным endpoint'ом.
            if (conversation.lastMessagePreview) {
                useParticipantsStore.getState().load(conversation.id).catch(() => {
                    /* fail-open: имя проступит после первого участникам-fetch retry */
                });
            }

            return conversation.id;
        } catch (error) {
            console.error("Open direct chat failed", error);
            return null;
        } finally {
            // Сбрасываем pending только если он указывал на этого юзера — иначе
            // мог уже стартовать запрос для другого user'а.
            if (get().pendingUserId === userId) {
                set({ pendingUserId: null });
            }
        }
    },

    clearGlobal: () => {
        get().globalAbort?.abort();
        get().usersAbort?.abort();
        set({
            globalScope: "messages",
            globalQuery: "",
            globalResults: [],
            globalNextCursor: undefined,
            isGlobalLoading: false,
            globalError: null,
            globalAbort: null,
            userResults: [],
            usersNextOffset: undefined,
            isUsersLoading: false,
            usersError: null,
            usersAbort: null,
            pendingUserId: null,
        });
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
