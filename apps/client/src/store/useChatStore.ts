import type { ChatMessageProps } from "@/entities/chat-message";
import { getMockMessagesForThread } from "@/mocks/messages";
import type { ChatThreadKind } from "@/shared/lib/chat-thread-key";
import { buildChatThreadKey } from "@/shared/lib/chat-thread-key";
import { create } from "zustand";
interface ChatState {
    messages: ChatMessageProps[];
    isLoading: boolean;
    hasMore: boolean;
    pageIndex: number;
    activeThreadKey: string | null;
    loadMessages: (chatId: string, kind: ChatThreadKind, reset?: boolean) => Promise<void>;
    loadMore: (chatId: string, kind: ChatThreadKind) => Promise<void>;
}
const PAGE_SIZE = 15;
function sliceNewestFirst(all: readonly ChatMessageProps[], pageIndex: number): ChatMessageProps[] {
    const end = Math.min((pageIndex + 1) * PAGE_SIZE, all.length);
    return all.slice(0, end) as ChatMessageProps[];
}
export const useChatStore = create<ChatState>((set, get) => ({
    messages: [],
    isLoading: false,
    hasMore: true,
    pageIndex: 0,
    activeThreadKey: null,
    loadMessages: async (chatId, kind, reset = false) => {
        const threadKey = buildChatThreadKey(kind, chatId);
        const { activeThreadKey, pageIndex } = get();
        const shouldReset = reset || activeThreadKey !== threadKey || activeThreadKey === null;
        const nextPageIndex = shouldReset ? 0 : pageIndex;
        set({ isLoading: true, activeThreadKey: threadKey });
        await new Promise((resolve) => setTimeout(resolve, 400));
        const all = getMockMessagesForThread(threadKey);
        const slice = sliceNewestFirst(all, nextPageIndex);
        const hasMore = slice.length < all.length;
        set({
            messages: slice,
            isLoading: false,
            hasMore,
            pageIndex: nextPageIndex,
        });
    },
    loadMore: async (chatId, kind) => {
        const { isLoading, hasMore, pageIndex, activeThreadKey, loadMessages } = get();
        const threadKey = buildChatThreadKey(kind, chatId);
        if (isLoading || !hasMore)
            return;
        if (activeThreadKey !== threadKey)
            return;
        set({ pageIndex: pageIndex + 1 });
        await loadMessages(chatId, kind, false);
    },
}));
