import type { ForwardedFromDto, ReactionsSummary, ReplyToDto } from "@urfu-link/api-client";

export interface Attachment {
    name: string;
    url: string;
}

/** Локальный статус для optimistic UI: undefined для серверных, остальные — клиентские. */
export type LocalDeliveryStatus = "sending" | "sent" | "failed";

export interface ChatMessageProps {
    id: string;
    text: string;
    isOwn: boolean;
    time: string;
    avatarUrl: string;
    showAvatar?: boolean;
    seen?: boolean;
    attachments?: Attachment[];
    replyTo?: ReplyToDto | null;
    reactions?: ReactionsSummary;
    editedAtUtc?: string | null;
    forwardedFrom?: ForwardedFromDto | null;
    isDeleted?: boolean;
    threadReplyCount?: number;
    localStatus?: LocalDeliveryStatus;
    onLongPress?: () => void;
    onThreadOpen?: () => void;
    onReactionPress?: (emoji: string) => void;
}
