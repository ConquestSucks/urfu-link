import {
    getDocumentVisibility,
    subscribeDocumentVisibility,
} from "../document-visibility";

describe("document visibility helpers", () => {
    const originalDocument = globalThis.document;

    afterEach(() => {
        Object.defineProperty(globalThis, "document", {
            configurable: true,
            value: originalDocument,
        });
        jest.restoreAllMocks();
    });

    it("treats native-like environments without document listeners as visible", () => {
        Object.defineProperty(globalThis, "document", {
            configurable: true,
            value: { visibilityState: "hidden" },
        });

        const onChange = jest.fn();
        const unsubscribe = subscribeDocumentVisibility(onChange);

        expect(getDocumentVisibility()).toBe(true);
        expect(onChange).not.toHaveBeenCalled();

        unsubscribe();
    });

    it("subscribes to web visibility changes", () => {
        const listeners = new Map<string, () => void>();
        const addEventListener = jest.fn((event: string, listener: () => void) => {
            listeners.set(event, listener);
        });
        const removeEventListener = jest.fn();
        Object.defineProperty(globalThis, "document", {
            configurable: true,
            value: {
                addEventListener,
                removeEventListener,
                visibilityState: "hidden",
            },
        });

        const onChange = jest.fn();
        const unsubscribe = subscribeDocumentVisibility(onChange);
        listeners.get("visibilitychange")?.();

        expect(onChange).toHaveBeenCalledWith(false);

        unsubscribe();
        expect(removeEventListener).toHaveBeenCalledWith(
            "visibilitychange",
            expect.any(Function),
        );
    });
});
