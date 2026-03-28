export type InboxChatProps = {
  id: string;
  avatarUrl: string;
  name: string;
  message: string;
  time: string;
  isActive?: boolean;
  onPress?: () => void;
  /** Тред дисциплины: счётчики в шапке (моки / API) */
  onlineCount?: number;
  totalCount?: number;
};
