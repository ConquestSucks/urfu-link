import { useCallback, useEffect, useRef } from "react";
import { useSearchStore } from "./search-store";

const DEBOUNCE_MS = 300;

/**
 * Local search hook — search within a specific conversation.
 */
export function useLocalSearch(conversationId: string) {
    const { localQuery, localResults, isLocalLoading, localNextCursor, setLocalQuery, searchLocal, loadMoreLocal, clearLocal } = useSearchStore();
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
        hasMore: !!localNextCursor,
        onQueryChange,
        loadMore: loadMoreLocal,
        clear: clearLocal,
    };
}

/**
 * Global search hook — search across all conversations.
 */
export function useGlobalSearch() {
    const { globalQuery, globalResults, isGlobalLoading, globalNextCursor, setGlobalQuery, searchGlobal, loadMoreGlobal } = useSearchStore();
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

    return {
        query: globalQuery,
        results: globalResults,
        isLoading: isGlobalLoading,
        hasMore: !!globalNextCursor,
        onQueryChange,
        loadMore: loadMoreGlobal,
    };
}
