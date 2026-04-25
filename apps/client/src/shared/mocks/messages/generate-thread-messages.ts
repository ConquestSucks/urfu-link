import type { ChatMessageProps } from "@/entities/chat-message";
const MSGS_PER_THREAD = 46;
function hashString(input: string): number {
    let h = 0;
    for (let i = 0; i < input.length; i += 1) {
        h = Math.imul(31, h) + input.charCodeAt(i);
        h |= 0;
    }
    return Math.abs(h);
}
export function generateThreadMessages(threadKey: string, total: number = MSGS_PER_THREAD): ChatMessageProps[] {
    const seed = hashString(threadKey);
    return Array.from({ length: total }, (_, i) => {
        const seq = total - i;
        const isOwn = (seed + i) % 3 === 0;
        const avatarId = (seed + seq) % 64;
        return {
            id: `${threadKey}#${seq}`,
            text: `Сообщение №${seq} в потоке «${threadKey}». Текст для проверки скролла и пагинации.`,
            isOwn,
            seen: (seed + i) % 2 === 0,
            time: `${String(9 + (i % 12)).padStart(2, "0")}:${String((seed + i * 7) % 60).padStart(2, "0")}`,
            avatarUrl: `https://i.pravatar.cc/150?u=${avatarId}`,
            attachments: [
                {
                    name: `Файл №${seq}.pdf`,
                    url: `https://example.com/file${seq}.pdf`,
                },
                {
                    name: `Файл №${seq}.docx`,
                    url: `https://example.com/file${seq}.docx`,
                },
            ],
        };
    });
}
