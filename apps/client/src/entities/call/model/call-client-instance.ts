const storageKey = "urfu-link:call-client-instance-id";

let memoryClientInstanceId: string | null = null;

const createClientInstanceId = () => {
    const cryptoApi = globalThis.crypto;
    if (typeof cryptoApi?.randomUUID === "function") {
        return cryptoApi.randomUUID();
    }

    return `call-client-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
};

export const getCallClientInstanceId = () => {
    if (memoryClientInstanceId) {
        return memoryClientInstanceId;
    }

    try {
        const existing = globalThis.sessionStorage?.getItem(storageKey);
        if (existing) {
            memoryClientInstanceId = existing;
            return existing;
        }

        const created = createClientInstanceId();
        globalThis.sessionStorage?.setItem(storageKey, created);
        memoryClientInstanceId = created;
        return created;
    } catch {
        memoryClientInstanceId = createClientInstanceId();
        return memoryClientInstanceId;
    }
};
