import { useEffect, useRef } from "react";
import { AppState, AppStateStatus } from "react-native";
import { usePresenceStore } from "@/entities/presence";

const HEARTBEAT_INTERVAL_MS = 15_000;

/**
 * Подключается к PresenceHub и поддерживает heartbeat раз в 15 секунд.
 * Монтировать только один раз в корневом лэйауте авторизованной зоны.
 */
export function usePresenceHub() {
    const { connect, disconnect, sendHeartbeat } = usePresenceStore();
    const heartbeatRef = useRef<ReturnType<typeof setInterval> | null>(null);

    const startHeartbeat = () => {
        if (heartbeatRef.current) return;
        sendHeartbeat(); // initial
        heartbeatRef.current = setInterval(() => {
            sendHeartbeat();
        }, HEARTBEAT_INTERVAL_MS);
    };

    const stopHeartbeat = () => {
        if (heartbeatRef.current) {
            clearInterval(heartbeatRef.current);
            heartbeatRef.current = null;
        }
    };

    useEffect(() => {
        connect().then(() => startHeartbeat());

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                connect().then(() => startHeartbeat());
            } else if (nextState === "background" || nextState === "inactive") {
                stopHeartbeat();
            }
        });

        return () => {
            stopHeartbeat();
            disconnect();
            subscription.remove();
        };
    }, []);
}
