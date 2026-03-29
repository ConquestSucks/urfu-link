import { usePathname } from "expo-router";
import { useMemo } from "react";

const CHAT_SEGMENT = /\/chats\/([^/]+)/;
const SUBJECT_SEGMENT = /\/subjects\/([^/]+)/;

export function useInboxPathIds() {
    const pathname = usePathname();
    return useMemo(
        () => ({
            chatId: pathname.match(CHAT_SEGMENT)?.[1],
            subjectThreadId: pathname.match(SUBJECT_SEGMENT)?.[1],
        }),
        [pathname],
    );
}
