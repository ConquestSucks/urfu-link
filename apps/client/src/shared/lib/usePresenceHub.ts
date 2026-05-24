import { useEffect, useRef } from "react";
import { AppState, AppStateStatus } from "react-native";
import { usePresenceStore } from "@/entities/presence";
import { notifyPresenceDisconnect } from "@/shared/lib/signalr";

const HEARTBEAT_INTERVAL_MS = 15_000;

/**
 * Подключается к PresenceHub и поддерживает heartbeat раз в 15 секунд.
 * Монтировать только один раз в корневом лэйауте авторизованной зоны.
 */
export function usePresenceHub() {
    const { connect, disconnect, sendHeartbeat } = usePresenceStore();
    const heartbeatRef = useRef<ReturnType<typeof setInterval> | null>(null);
    const isForegroundRef = useRef(true);
    const disconnectPromiseRef = useRef<Promise<void> | null>(null);

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

    const connectAndStartHeartbeat = () => {
        isForegroundRef.current = true;
        const pendingDisconnect = disconnectPromiseRef.current ?? Promise.resolve();

        void pendingDisconnect.then(() => {
            if (!isForegroundRef.current) return Promise.resolve();
            return connect();
        }).then(() => {
            if (isForegroundRef.current) {
                startHeartbeat();
            }
        });
    };

    const disconnectAndStopHeartbeat = () => {
        isForegroundRef.current = false;
        stopHeartbeat();
        if (disconnectPromiseRef.current) return;

        const promise = Promise.resolve(disconnect())
            .catch((error) => {
                console.warn("PresenceHub disconnect failed", error);
            })
            .finally(() => {
                if (disconnectPromiseRef.current === promise) {
                    disconnectPromiseRef.current = null;
                }
            });
        disconnectPromiseRef.current = promise;
    };

    useEffect(() => {
        connectAndStartHeartbeat();

        const handlePageExit = () => {
            notifyPresenceDisconnect();
            disconnectAndStopHeartbeat();
        };

        if (typeof window !== "undefined") {
            window.addEventListener("pagehide", handlePageExit);
            window.addEventListener("beforeunload", handlePageExit);
        }

        const subscription = AppState.addEventListener("change", (nextState: AppStateStatus) => {
            if (nextState === "active") {
                connectAndStartHeartbeat();
            } else if (nextState === "background" || nextState === "inactive") {
                disconnectAndStopHeartbeat();
            }
        });

        return () => {
            if (typeof window !== "undefined") {
                window.removeEventListener("pagehide", handlePageExit);
                window.removeEventListener("beforeunload", handlePageExit);
            }
            disconnectAndStopHeartbeat();
            subscription.remove();
        };
    }, []);
}
