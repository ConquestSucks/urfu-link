import type { ForwardedFromDto, ReactionsSummary, ReplyToDto } from "@urfu-link/api-client";

export interface Attachment {
    name: string;
    url: string;
}

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
    onLongPress?: () => void;
    onThreadOpen?: () => void;
}
