export type InboxChatProps = {
    id: string;
    avatarUrl: string;
    name: string;
    message: string;
    time: string;
    isActive?: boolean;
    onPress?: () => void;
    onlineCount?: number;
    totalCount?: number;
    /** Сообщений от собеседника, которые вы ещё не открывали */
    unreadCount?: number;
    /** Последнее сообщение в превью — от вас (показываем галочки) */
    lastMessageFromSelf?: boolean;
    /** Прочитано собеседником (синие галочки); иначе серые */
    lastMessageRead?: boolean;
};
