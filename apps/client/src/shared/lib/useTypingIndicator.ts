import { useCallback, useRef } from "react";
import { usePresenceStore } from "@/entities/presence";

const STOP_TYPING_DELAY_MS = 2000;

/**
 * Хук для управления индикатором ввода собеседника.
 * @param conversationId - ID текущего диалога
 */
export function useTypingIndicator(conversationId: string) {
    const { startTyping, stopTyping } = usePresenceStore();
    const typingTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const isTypingRef = useRef(false);

    const onTextChange = useCallback(
        (text: string) => {
            if (!conversationId) return;

            if (text.length > 0) {
                if (!isTypingRef.current) {
                    isTypingRef.current = true;
                    startTyping(conversationId);
                }

                // Reset the stop timer on each keystroke
                if (typingTimer.current) clearTimeout(typingTimer.current);
                typingTimer.current = setTimeout(() => {
                    isTypingRef.current = false;
                    stopTyping(conversationId);
                    typingTimer.current = null;
                }, STOP_TYPING_DELAY_MS);
            } else {
                // Cleared the input
                if (typingTimer.current) clearTimeout(typingTimer.current);
                if (isTypingRef.current) {
                    isTypingRef.current = false;
                    stopTyping(conversationId);
                }
            }
        },
        [conversationId, startTyping, stopTyping]
    );

    const onSend = useCallback(() => {
        if (typingTimer.current) clearTimeout(typingTimer.current);
        if (isTypingRef.current) {
            isTypingRef.current = false;
            stopTyping(conversationId);
        }
    }, [conversationId, stopTyping]);

    return { onTextChange, onSend };
}
