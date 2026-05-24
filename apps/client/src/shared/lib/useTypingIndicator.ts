import { useCallback, useRef } from "react";
import { useChatStore } from "@/entities/conversation/model/chat-store";

const STOP_TYPING_DELAY_MS = 2000;

/**
 * Хук для управления индикатором ввода собеседника.
 * @param conversationId - ID текущего диалога
 */
interface UseTypingIndicatorOptions {
    enabled?: boolean;
}

export function useTypingIndicator(
    conversationId: string,
    { enabled = true }: UseTypingIndicatorOptions = {},
) {
    const { startTyping, stopTyping } = useChatStore();
    const typingTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const isTypingRef = useRef(false);

    const onTextChange = useCallback(
        (text: string) => {
            if (!enabled || !conversationId) return;

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
        [conversationId, enabled, startTyping, stopTyping]
    );

    const onSend = useCallback(() => {
        if (typingTimer.current) clearTimeout(typingTimer.current);
        if (enabled && isTypingRef.current) {
            isTypingRef.current = false;
            stopTyping(conversationId);
        }
    }, [conversationId, enabled, stopTyping]);

    return { onTextChange, onSend };
}
