import { useCallback, useEffect, useMemo, useState } from "react";
import { Pressable, Text, View } from "react-native";
import { useRouter } from "expo-router";
import { useCallStore } from "@/entities/call";
import { getCallClientInstanceId } from "@/entities/call/model/call-client-instance";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { PhoneIcon } from "@/shared/ui/phosphor";

const channelName = "urfu-link:call-tabs";
const activeCallStorageKey = "urfu-link:active-call-tab";
const activeCallTtlMs = 8_000;
const heartbeatMs = 2_000;

type ActiveCallRecord = {
    callId: string;
    clientInstanceId: string;
    updatedAt: number;
};

type ActiveCallMessage = ActiveCallRecord & { type: "active-call" };
type CallTabMessage = ActiveCallMessage;

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
    const router = useRouter();
    const activeCall = useCallStore((state) => state.activeCall);
    const { isMobile } = useWindowSize();
    const clientInstanceId = useMemo(() => getCallClientInstanceId(), []);
    const [remoteCall, setRemoteCall] = useState<ActiveCallRecord | null>(null);
    const [dismissedKey, setDismissedKey] = useState<string | null>(null);

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

        const handleMessage = (event: MessageEvent<Partial<CallTabMessage>>) => {
            const message = event.data;
            if (
                message.type !== "active-call"
                || !message.callId
                || !message.clientInstanceId
                || typeof message.updatedAt !== "number"
            ) {
                return;
            }

            if (message.clientInstanceId === clientInstanceId) {
                setRemoteCall(null);
                return;
            }

            setRemoteCall({
                callId: message.callId,
                clientInstanceId: message.clientInstanceId,
                updatedAt: message.updatedAt,
            });
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
    }, [clientInstanceId, updateRemoteFromStorage]);

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
            channel?.postMessage({ ...record, type: "active-call" } satisfies ActiveCallMessage);
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

    const handleTakeover = () => {
        router.push(`/call/${remoteCall.callId}` as never);
    };

    const title = isMobile
        ? "Звонок идет на другом устройстве"
        : "Звонок идет в другой вкладке";
    const description = isMobile
        ? "Можно перехватить звонок здесь или оставить его на другом устройстве."
        : "Можно перехватить звонок здесь или оставить его в активной вкладке.";

    return (
        <View
            testID="call-tab-coordinator"
            className="absolute left-4 right-4 bottom-5 rounded-2xl border border-white/10 bg-app-card/95 px-4 py-3 shadow-lg"
            style={{ zIndex: 30, elevation: 30 }}
        >
            <View className="flex-row items-start gap-3">
                <View className="h-9 w-9 rounded-xl bg-brand-650/15 items-center justify-center">
                    <PhoneIcon size={18} className="text-brand-300" />
                </View>
                <View className="flex-1">
                    <Text className="text-sm font-semibold text-white">
                        {title}
                    </Text>
                    <Text className="mt-1 text-xs leading-5 text-text-muted">
                        {description}
                    </Text>
                </View>
            </View>
            <View className="mt-3 flex-row gap-2">
                <Pressable
                    accessibilityRole="button"
                    className="flex-1 rounded-xl bg-brand-650 px-3 py-2.5 items-center active:opacity-80"
                    onPress={handleTakeover}
                    testID="call-tab-takeover"
                >
                    <Text className="text-sm font-semibold text-white">
                        Перехватить
                    </Text>
                </Pressable>
                <Pressable
                    accessibilityRole="button"
                    className="rounded-xl border border-white/10 bg-white/5 px-3 py-2.5 items-center active:bg-white/10"
                    onPress={() => setDismissedKey(remoteKey)}
                    testID="call-tab-dismiss"
                >
                    <Text className="text-sm font-semibold text-text-secondary">
                        Оставить
                    </Text>
                </Pressable>
            </View>
        </View>
    );
};
