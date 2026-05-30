import { useCallback, useEffect, useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";
import { useCallStore } from "@/entities/call";
import { getCallClientInstanceId } from "@/entities/call/model/call-client-instance";

const channelName = "urfu-link:call-tabs";
const activeCallStorageKey = "urfu-link:active-call-tab";
const activeCallTtlMs = 8_000;
const heartbeatMs = 2_000;

type ActiveCallRecord = {
    callId: string;
    clientInstanceId: string;
    updatedAt: number;
};

type CallTabMessage =
    | (ActiveCallRecord & { type: "active-call" })
    | { type: "focus-request"; targetClientInstanceId: string };

const hasBrowserTabApis = () =>
    typeof globalThis.window !== "undefined"
    && typeof globalThis.localStorage !== "undefined";

const readActiveCallRecord = (): ActiveCallRecord | null => {
    if (!hasBrowserTabApis()) {
        return null;
    }

    try {
        const raw = globalThis.localStorage.getItem(activeCallStorageKey);
        if (!raw) {
            return null;
        }

        const parsed = JSON.parse(raw) as Partial<ActiveCallRecord>;
        if (!parsed.callId || !parsed.clientInstanceId || typeof parsed.updatedAt !== "number") {
            return null;
        }

        return Date.now() - parsed.updatedAt <= activeCallTtlMs
            ? {
                callId: parsed.callId,
                clientInstanceId: parsed.clientInstanceId,
                updatedAt: parsed.updatedAt,
            }
            : null;
    } catch {
        return null;
    }
};

const writeActiveCallRecord = (record: ActiveCallRecord) => {
    if (!hasBrowserTabApis()) {
        return;
    }

    try {
        globalThis.localStorage.setItem(activeCallStorageKey, JSON.stringify(record));
    } catch {
        // localStorage can be blocked in private modes; BroadcastChannel still covers live tabs.
    }
};

const clearOwnActiveCallRecord = (clientInstanceId: string) => {
    const current = readActiveCallRecord();
    if (current?.clientInstanceId !== clientInstanceId) {
        return;
    }

    try {
        globalThis.localStorage.removeItem(activeCallStorageKey);
    } catch {
        // ignore storage cleanup failures
    }
};

const createChannel = () => {
    if (typeof globalThis.BroadcastChannel === "undefined") {
        return null;
    }

    return new globalThis.BroadcastChannel(channelName);
};

export const CallTabCoordinator = () => {
    const activeCall = useCallStore((state) => state.activeCall);
    const clientInstanceId = useMemo(() => getCallClientInstanceId(), []);
    const [remoteCall, setRemoteCall] = useState<ActiveCallRecord | null>(null);
    const [dismissedKey, setDismissedKey] = useState<string | null>(null);
    const [focusHint, setFocusHint] = useState(false);

    const updateRemoteFromStorage = useCallback(() => {
        const record = readActiveCallRecord();
        setRemoteCall(
            record && record.clientInstanceId !== clientInstanceId
                ? record
                : null,
        );
    }, [clientInstanceId]);

    useEffect(() => {
        if (!hasBrowserTabApis()) {
            return undefined;
        }

        updateRemoteFromStorage();
        const interval = globalThis.setInterval(updateRemoteFromStorage, heartbeatMs);
        const channel = createChannel();

        const handleMessage = (event: MessageEvent<CallTabMessage>) => {
            const message = event.data;
            if (message.type === "focus-request") {
                if (
                    message.targetClientInstanceId === clientInstanceId
                    && activeCall?.status === "Active"
                ) {
                    globalThis.window.focus();
                }
                return;
            }

            if (message.clientInstanceId === clientInstanceId) {
                setRemoteCall(null);
                return;
            }

            setRemoteCall(message);
        };

        const handleStorage = (event: StorageEvent) => {
            if (event.key === activeCallStorageKey) {
                updateRemoteFromStorage();
            }
        };

        channel?.addEventListener("message", handleMessage);
        globalThis.window.addEventListener("storage", handleStorage);

        return () => {
            globalThis.clearInterval(interval);
            channel?.removeEventListener("message", handleMessage);
            channel?.close();
            globalThis.window.removeEventListener("storage", handleStorage);
        };
    }, [activeCall?.status, clientInstanceId, updateRemoteFromStorage]);

    useEffect(() => {
        if (!hasBrowserTabApis() || activeCall?.status !== "Active") {
            clearOwnActiveCallRecord(clientInstanceId);
            return undefined;
        }

        const channel = createChannel();
        const publish = () => {
            const record = {
                callId: activeCall.id,
                clientInstanceId,
                updatedAt: Date.now(),
            };
            writeActiveCallRecord(record);
            channel?.postMessage({ ...record, type: "active-call" } satisfies CallTabMessage);
        };

        publish();
        const interval = globalThis.setInterval(publish, heartbeatMs);

        return () => {
            globalThis.clearInterval(interval);
            channel?.close();
            clearOwnActiveCallRecord(clientInstanceId);
        };
    }, [activeCall?.id, activeCall?.status, clientInstanceId]);

    if (!remoteCall || activeCall?.id === remoteCall.callId) {
        return null;
    }

    const remoteKey = `${remoteCall.callId}:${remoteCall.clientInstanceId}`;
    if (dismissedKey === remoteKey) {
        return null;
    }

    const handleFocusRequest = () => {
        setFocusHint(true);
        const channel = createChannel();
        channel?.postMessage({
            type: "focus-request",
            targetClientInstanceId: remoteCall.clientInstanceId,
        } satisfies CallTabMessage);
        channel?.close();
    };

    return (
        <View
            testID="call-tab-coordinator"
            className="absolute left-4 right-4 bottom-5 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 shadow-lg"
            style={{ zIndex: 30, elevation: 30 }}
        >
            <Text className="text-sm font-semibold text-amber-950">
                Звонок идет в другой вкладке
            </Text>
            <Text className="mt-1 text-xs text-amber-900">
                Можно переключиться на активную вкладку. Если браузер не даст ее сфокусировать, откройте ее вручную.
            </Text>
            {focusHint && (
                <Text className="mt-2 text-xs text-amber-900">
                    Запрос на переключение отправлен.
                </Text>
            )}
            <View className="mt-3 flex-row gap-2">
                <Pressable
                    accessibilityRole="button"
                    className="rounded-md bg-amber-900 px-3 py-2"
                    onPress={handleFocusRequest}
                    testID="call-tab-focus"
                >
                    <Text className="text-xs font-semibold text-white">Перейти</Text>
                </Pressable>
                <Pressable
                    accessibilityRole="button"
                    className="rounded-md border border-amber-300 px-3 py-2"
                    onPress={() => setDismissedKey(remoteKey)}
                    testID="call-tab-dismiss"
                >
                    <Text className="text-xs font-semibold text-amber-950">Скрыть</Text>
                </Pressable>
            </View>
        </View>
    );
};
