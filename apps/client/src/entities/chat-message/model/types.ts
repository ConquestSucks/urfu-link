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
}

