import { useCallback, useEffect, useRef } from "react";
import { useSearchStore } from "./search-store";

const DEBOUNCE_MS = 300;

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
        setLocalQuery,
        searchLocal,
        loadMoreLocal,
        clearLocal,
    } = useSearchStore();
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
        onQueryChange,
        loadMore: loadMoreLocal,
        retry,
        clear: clearLocal,
    };
}

/**
 * Global search hook — search across all conversations.
 */
export function useGlobalSearch() {
    const {
        globalQuery,
        globalResults,
        isGlobalLoading,
        globalError,
        globalNextCursor,
        setGlobalQuery,
        searchGlobal,
        loadMoreGlobal,
    } = useSearchStore();
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    const onQueryChange = useCallback(
        (query: string) => {
            setGlobalQuery(query);
            if (debounceTimer.current) clearTimeout(debounceTimer.current);
            debounceTimer.current = setTimeout(() => {
                searchGlobal(query);
            }, DEBOUNCE_MS);
        },
        [searchGlobal, setGlobalQuery]
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
        onQueryChange,
        loadMore: loadMoreGlobal,
        retry,
    };
}
