export interface InboxNotificationProps {
    id: string;
    title: string;
    description: string;
    time: string;
    scope: "chats" | "subjects";
    /** false — непрочитано (акцент в списке) */
    isRead?: boolean;
}
