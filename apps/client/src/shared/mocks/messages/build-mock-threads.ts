import type { ChatMessageProps } from "@/entities/chat-message";
import { buildChatThreadKey } from "@/shared/lib/chat-thread-key";
import { chatsMockData } from "../chats";
import { subjectsMockData } from "../subjects";
import { generateThreadMessages } from "./generate-thread-messages";
function buildMockMessagesByThread(): Record<string, ChatMessageProps[]> {
    const map: Record<string, ChatMessageProps[]> = {};
    for (const chat of chatsMockData) {
        const key = buildChatThreadKey("chat", chat.id);
        map[key] = generateThreadMessages(key);
    }
    for (const subject of subjectsMockData) {
        for (const thread of subject.messages) {
            const key = buildChatThreadKey("subject", thread.id);
            map[key] = generateThreadMessages(key);
        }
    }
    return map;
}
let cache: Record<string, ChatMessageProps[]> | null = null;
export function getMockMessagesByThread(): Readonly<Record<string, readonly ChatMessageProps[]>> {
    if (!cache) {
        cache = buildMockMessagesByThread();
    }
    return cache;
}
export function getMockMessagesForThread(threadKey: string): readonly ChatMessageProps[] {
    return getMockMessagesByThread()[threadKey] ?? [];
}
