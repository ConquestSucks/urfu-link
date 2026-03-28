export interface InboxSidebarListProps<T> {
  data: T[];
  renderItem: (item: T) => React.ReactNode;
}

export type TabType = "chats" | "notifications";
