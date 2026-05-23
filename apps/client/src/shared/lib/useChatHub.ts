import { useEffect } from "react";
import { AppState, AppStateStatus } from "react-native";
import { useChatStore } from "@/entities/conversation/model/chat-store";

/**
 * Подключается к ChatHub при mount, disconnect при unmount.
 * Reconnect при возврате приложения в foreground.
 * Монтировать только один раз в корневом лэйауте авторизованной зоны.
 */
export const useChatHub = () => {
    const { connect, disconnect } = useChatStore();

    useEffect(() => {
        connect();

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                connect();
            }
            // На background соединение не рвём — .withAutomaticReconnect() сам разберётся
            // с разрывом, если он случится.
        });

        return () => {
            disconnect();
            subscription.remove();
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
};
