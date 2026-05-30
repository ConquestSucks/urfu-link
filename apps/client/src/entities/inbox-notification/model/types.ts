export interface InboxNotificationProps {
    id: string;
    title: string;
    description: string;
    time: string;
    scope: "chats" | "subjects";
    deepLink?: string | null;
    data?: Record<string, string>;
    actorName?: string | null;
    /** false — непрочитано (акцент в списке) */
    isRead?: boolean;
    onMarkRead?: (id: string) => void;
    onPress?: () => void;
}
