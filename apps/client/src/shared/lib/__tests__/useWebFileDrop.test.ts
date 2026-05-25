import { act, renderHook } from "@testing-library/react-native";
import { Platform } from "react-native";

import { useWebFileDrop } from "../useWebFileDrop";

type ListenerMap = Record<string, EventListener>;

const createTarget = () => {
    const listeners: ListenerMap = {};
    const target = {
        addEventListener: jest.fn((event: string, listener: EventListener) => {
            listeners[event] = listener;
        }),
        removeEventListener: jest.fn((event: string, listener: EventListener) => {
            if (listeners[event] === listener) delete listeners[event];
        }),
    };

    return { target, listeners };
};

const createDragEvent = (files: File[] = []) => ({
    dataTransfer: {
        files,
        types: files.length > 0 ? ["Files"] : [],
        dropEffect: "none",
    },
    preventDefault: jest.fn(),
    stopPropagation: jest.fn(),
}) as unknown as DragEvent & {
    dataTransfer: DataTransfer & { dropEffect: string };
    preventDefault: jest.Mock;
    stopPropagation: jest.Mock;
};

describe("useWebFileDrop", () => {
    const originalPlatformOS = Platform.OS;

    beforeEach(() => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: "web" });
        jest.clearAllMocks();
    });

    afterEach(() => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: originalPlatformOS });
    });

    it("attaches DOM drop listeners and forwards files", () => {
        const onFiles = jest.fn();
        const onDragStateChange = jest.fn();
        const { target, listeners } = createTarget();
        const { result } = renderHook(() =>
            useWebFileDrop({ enabled: true, onFiles, onDragStateChange }),
        );

        act(() => {
            result.current.dropRef(target);
        });

        expect(target.addEventListener).toHaveBeenCalledWith("dragenter", expect.any(Function));
        expect(target.addEventListener).toHaveBeenCalledWith("dragover", expect.any(Function));
        expect(target.addEventListener).toHaveBeenCalledWith("dragleave", expect.any(Function));
        expect(target.addEventListener).toHaveBeenCalledWith("drop", expect.any(Function));

        const file = { name: "lecture.pdf" } as File;
        const dragOver = createDragEvent([file]);
        act(() => {
            listeners.dragover(dragOver);
        });
        expect(dragOver.preventDefault).toHaveBeenCalled();
        expect(dragOver.stopPropagation).toHaveBeenCalled();
        expect(dragOver.dataTransfer.dropEffect).toBe("copy");

        const drop = createDragEvent([file]);
        act(() => {
            listeners.drop(drop);
        });
        expect(onFiles).toHaveBeenCalledWith([file]);
        expect(result.current.isDragging).toBe(false);

        const dragEnter = createDragEvent([file]);
        act(() => {
            listeners.dragenter(dragEnter);
        });
        expect(result.current.isDragging).toBe(true);
        expect(onDragStateChange).toHaveBeenLastCalledWith(true);
    });

    it("removes DOM listeners on cleanup", () => {
        const { target } = createTarget();
        const { result, unmount } = renderHook(() =>
            useWebFileDrop({ enabled: true, onFiles: jest.fn() }),
        );

        act(() => {
            result.current.dropRef(target);
        });

        unmount();

        expect(target.removeEventListener).toHaveBeenCalledWith("dragenter", expect.any(Function));
        expect(target.removeEventListener).toHaveBeenCalledWith("dragover", expect.any(Function));
        expect(target.removeEventListener).toHaveBeenCalledWith("dragleave", expect.any(Function));
        expect(target.removeEventListener).toHaveBeenCalledWith("drop", expect.any(Function));
    });

    it("does not attach listeners outside web or when disabled", () => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: "ios" });
        const { target } = createTarget();
        const { result } = renderHook(() =>
            useWebFileDrop({ enabled: true, onFiles: jest.fn() }),
        );

        act(() => {
            result.current.dropRef(target);
        });

        expect(target.addEventListener).not.toHaveBeenCalled();
    });
});
