import type { ConversationPreview } from "@urfu-link/api-client";

const DIRECT_DRAFT_LOCAL_STATUS = "draft";

type DirectDraftAwareConversation = Pick<ConversationPreview, "type" | "lastMessagePreview"> & {
    _localStatus?: typeof DIRECT_DRAFT_LOCAL_STATUS;
};

export const isDirectDraftConversation = (
    conversation: DirectDraftAwareConversation | null | undefined,
) =>
    conversation?.type === "Direct" &&
    (conversation._localStatus === DIRECT_DRAFT_LOCAL_STATUS || !conversation.lastMessagePreview);

export const withDirectDraftStatus = (conversation: ConversationPreview): ConversationPreview =>
    ({
        ...conversation,
        _localStatus: DIRECT_DRAFT_LOCAL_STATUS,
    }) as ConversationPreview;

export const normalizeConversationDraftStatus = (
    conversation: ConversationPreview,
): ConversationPreview => {
    if (conversation.type === "Direct" && !conversation.lastMessagePreview) {
        return withDirectDraftStatus(conversation);
    }

    return withoutDirectDraftStatus(conversation);
};

export const withoutDirectDraftStatus = (conversation: ConversationPreview): ConversationPreview => {
    const { _localStatus: _ignored, ...rest } = conversation as ConversationPreview & {
        _localStatus?: typeof DIRECT_DRAFT_LOCAL_STATUS;
    };
    return rest;
};
