export type ChatThreadKind = "chat" | "subject";
export function buildChatThreadKey(kind: ChatThreadKind, id: string): string {
    return `${kind}:${id}`;
}
