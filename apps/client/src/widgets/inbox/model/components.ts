export interface InboxListProps<T> {
    data: T[];
    renderItem: (item: T) => React.ReactNode;
}
