import { useCallback, useEffect, useRef } from "react";
import { useShallow } from "zustand/react/shallow";
import { SearchFilterValues, useSearchStore } from "./search-store";

const DEBOUNCE_MS = 300;

// Zustand v5 запрещает деструктуризацию `useStore()` без selector-а — это даёт
// нестабильный `getSnapshot` и React-инфинит-луп при ре-рендерах. Все ниже —
// объект-selector + useShallow, чтобы подписка ре-рендерила хук только при
// фактическом изменении выбранных полей.

/**
 * Local search hook — search within a specific conversation.
 */
export function useLocalSearch(conversationId: string) {
    const {
        localQuery,
        localResults,
        isLocalLoading,
        localError,
        localNextCursor,
        localFilters,
        setLocalQuery,
        setLocalFilters,
        searchLocal,
        loadMoreLocal,
        clearLocal,
    } = useSearchStore(
        useShallow((s) => ({
            localQuery: s.localQuery,
            localResults: s.localResults,
            isLocalLoading: s.isLocalLoading,
            localError: s.localError,
            localNextCursor: s.localNextCursor,
            localFilters: s.localFilters,
            setLocalQuery: s.setLocalQuery,
            setLocalFilters: s.setLocalFilters,
            searchLocal: s.searchLocal,
            loadMoreLocal: s.loadMoreLocal,
            clearLocal: s.clearLocal,
        })),
    );
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    const onQueryChange = useCallback(
        (query: string) => {
            setLocalQuery(query);
            if (debounceTimer.current) clearTimeout(debounceTimer.current);
            debounceTimer.current = setTimeout(() => {
                searchLocal(conversationId, query);
            }, DEBOUNCE_MS);
        },
        [conversationId, searchLocal, setLocalQuery]
    );

    const onFiltersChange = useCallback(
        (next: SearchFilterValues) => setLocalFilters(next),
        [setLocalFilters],
    );

    const retry = useCallback(() => {
        if (localQuery.length >= 2) {
            searchLocal(conversationId, localQuery);
        }
    }, [conversationId, localQuery, searchLocal]);

    useEffect(() => {
        return () => {
            if (debounceTimer.current) clearTimeout(debounceTimer.current);
            clearLocal();
        };
    }, [conversationId]);

    return {
        query: localQuery,
        results: localResults,
        isLoading: isLocalLoading,
        error: localError,
        hasMore: !!localNextCursor,
        filters: localFilters,
        onQueryChange,
        onFiltersChange,
        loadMore: loadMoreLocal,
        retry,
        clear: clearLocal,
    };
}

/**
 * Global search hook — search across all conversations.
 *
 * Управляет одним SearchBar-ом, но в зависимости от globalScope диспатчит запрос
 * либо в searchGlobal (сообщения), либо в searchUsers (пользователи). Это позволяет
 * пользователю печатать раз и переключать табы без перетипирования запроса.
 */
export function useGlobalSearch() {
    const {
        globalScope,
        globalQuery,
        globalResults,
        isGlobalLoading,
        globalError,
        globalNextCursor,
        globalFilters,
        setGlobalQuery,
        setGlobalFilters,
        searchGlobal,
        loadMoreGlobal,
        searchUsers,
    } = useSearchStore(
        useShallow((s) => ({
            globalScope: s.globalScope,
            globalQuery: s.globalQuery,
            globalResults: s.globalResults,
            isGlobalLoading: s.isGlobalLoading,
            globalError: s.globalError,
            globalNextCursor: s.globalNextCursor,
            globalFilters: s.globalFilters,
            setGlobalQuery: s.setGlobalQuery,
            setGlobalFilters: s.setGlobalFilters,
            searchGlobal: s.searchGlobal,
            loadMoreGlobal: s.loadMoreGlobal,
            searchUsers: s.searchUsers,
        })),
    );
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    const onQueryChange = useCallback(
        (query: string) => {
            setGlobalQuery(query);
            if (debounceTimer.current) clearTimeout(debounceTimer.current);
            debounceTimer.current = setTimeout(() => {
                if (globalScope === "users") {
                    searchUsers(query);
                } else {
                    searchGlobal(query);
                }
            }, DEBOUNCE_MS);
        },
        [globalScope, searchGlobal, searchUsers, setGlobalQuery]
    );

    const onFiltersChange = useCallback(
        (next: SearchFilterValues) => setGlobalFilters(next),
        [setGlobalFilters],
    );

    const retry = useCallback(() => {
        if (globalQuery.length >= 2) {
            searchGlobal(globalQuery);
        }
    }, [globalQuery, searchGlobal]);

    return {
        query: globalQuery,
        results: globalResults,
        isLoading: isGlobalLoading,
        error: globalError,
        hasMore: !!globalNextCursor,
        filters: globalFilters,
        onQueryChange,
        onFiltersChange,
        loadMore: loadMoreGlobal,
        retry,
    };
}

/**
 * User search hook — отдаёт результаты глобального поиска пользователей.
 *
 * Используется внутри UserSearchResults, дёргает store-ветку userResults.
 * onQueryChange / debounce здесь не нужны — поиск стартуется из useGlobalSearch.
 */
export function useUserSearch() {
    const {
        globalQuery,
        userResults,
        isUsersLoading,
        usersError,
        usersNextOffset,
        pendingUserId,
        searchUsers,
        loadMoreUsers,
        openDirectWithUser,
    } = useSearchStore(
        useShallow((s) => ({
            globalQuery: s.globalQuery,
            userResults: s.userResults,
            isUsersLoading: s.isUsersLoading,
            usersError: s.usersError,
            usersNextOffset: s.usersNextOffset,
            pendingUserId: s.pendingUserId,
            searchUsers: s.searchUsers,
            loadMoreUsers: s.loadMoreUsers,
            openDirectWithUser: s.openDirectWithUser,
        })),
    );

    const retry = useCallback(() => {
        if (globalQuery.length >= 2) {
            searchUsers(globalQuery);
        }
    }, [globalQuery, searchUsers]);

    return {
        query: globalQuery,
        results: userResults,
        isLoading: isUsersLoading,
        error: usersError,
        hasMore: usersNextOffset !== undefined,
        pendingUserId,
        loadMore: loadMoreUsers,
        retry,
        openDirectWithUser,
    };
}
