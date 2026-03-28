/**
 * Различает потоки сообщений при совпадении сырого id
 * (например личный чат `1` и тред дисциплины `1`).
 */
export type ChatThreadKind = "chat" | "subject";

export function buildChatThreadKey(
  kind: ChatThreadKind,
  id: string,
): string {
  return `${kind}:${id}`;
}
