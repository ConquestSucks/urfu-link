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

        const handlePageHide = () => {
            void disconnect();
        };

        const handlePageShow = () => {
            connect();
        };

        if (typeof window !== "undefined") {
            window.addEventListener("pagehide", handlePageHide);
            window.addEventListener("pageshow", handlePageShow);
        }

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                connect();
            }
            // На background соединение не рвём — .withAutomaticReconnect() сам разберётся
            // с разрывом, если он случится.
        });

        return () => {
            if (typeof window !== "undefined") {
                window.removeEventListener("pagehide", handlePageHide);
                window.removeEventListener("pageshow", handlePageShow);
            }
            disconnect();
            subscription.remove();
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
};
