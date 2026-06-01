import { useEffect } from "react";
import { AppState, AppStateStatus } from "react-native";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { subscribePageLifecycle } from "@/shared/lib/page-lifecycle";

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

        const unsubscribePageLifecycle = subscribePageLifecycle({
            onPageHide: handlePageHide,
            onPageShow: handlePageShow,
        });

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                connect();
            }
            // На background соединение не рвём — .withAutomaticReconnect() сам разберётся
            // с разрывом, если он случится.
        });

        return () => {
            unsubscribePageLifecycle();
            disconnect();
            subscription.remove();
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
};
