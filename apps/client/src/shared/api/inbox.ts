import { InboxChatProps } from "@/entities/inbox-chat";
import { chatsMockData, notificationsMockData, subjectsMockData, } from "@/mocks";
const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
export const inboxApi = {
    getChats: async () => {
        await delay(500);
        return chatsMockData;
    },
    getSubjects: async () => {
        await delay(500);
        return subjectsMockData;
    },
    getNotifications: async () => {
        await delay(500);
        return notificationsMockData;
    },
    getChatMeta: async (id: string, type: "chat" | "subject"): Promise<InboxChatProps | undefined> => {
        await delay(300);
        if (type === "chat") {
            return chatsMockData.find((chat) => chat.id === id);
        }
        if (type === "subject") {
            for (const subject of subjectsMockData) {
                const found = subject.messages.find((msg) => msg.id === id);
                if (found)
                    return found;
            }
        }
        return undefined;
    },
};
