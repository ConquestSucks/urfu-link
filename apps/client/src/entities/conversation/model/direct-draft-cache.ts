import type { ConversationParticipantDto, ConversationPreview } from "@urfu-link/api-client";
import { appStorage } from "@/shared/lib/storage";
import { isDirectDraftConversation } from "./direct-draft-status";
export { isDirectDraftConversation } from "./direct-draft-status";

const STORAGE_KEY = "chat.directDrafts.v1";
const MAX_CACHED_DRAFTS = 20;
const DIRECT_DRAFT_ID_PATTERN = /^[0-9a-f]{40}$/i;

type DirectDraftCacheEntry = {
    conversation: ConversationPreview;
    participants: ConversationParticipantDto[];
    savedAt: number;
};

type DirectDraftCache = Record<string, DirectDraftCacheEntry>;

export const isDirectDraftId = (value: string | null | undefined): value is string =>
    typeof value === "string" && DIRECT_DRAFT_ID_PATTERN.test(value);

const readCache = (): DirectDraftCache => {
    const raw = appStorage.getItem(STORAGE_KEY);
    if (!raw || typeof raw !== "string") {
        return {};
    }

    try {
        const parsed = JSON.parse(raw) as DirectDraftCache;
        return parsed && typeof parsed === "object" ? parsed : {};
    } catch {
        return {};
    }
};

const writeCache = (cache: DirectDraftCache) => {
    appStorage.setItem(STORAGE_KEY, JSON.stringify(cache));
};

export const saveDirectDraftConversation = (
    conversation: ConversationPreview,
    participants: ConversationParticipantDto[],
) => {
    if (!isDirectDraftId(conversation.id) || !isDirectDraftConversation(conversation)) {
        return;
    }

    const cache = readCache();
    cache[conversation.id] = {
        conversation,
        participants,
        savedAt: Date.now(),
    };

    const pruned = Object.fromEntries(
        Object.entries(cache)
            .sort(([, left], [, right]) => right.savedAt - left.savedAt)
            .slice(0, MAX_CACHED_DRAFTS),
    );

    writeCache(pruned);
};

export const restoreDirectDraftConversation = (
    conversationId: string,
): Pick<DirectDraftCacheEntry, "conversation" | "participants"> | null => {
    if (!isDirectDraftId(conversationId)) {
        return null;
    }

    const entry = readCache()[conversationId];
    if (!entry || !isDirectDraftConversation(entry.conversation)) {
        return null;
    }

    return {
        conversation: entry.conversation,
        participants: entry.participants,
    };
};
