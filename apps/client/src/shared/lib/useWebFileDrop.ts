import { useCallback, useEffect, useRef, useState } from "react";
import { Platform } from "react-native";

type FileDropTarget = {
    addEventListener: EventTarget["addEventListener"];
    removeEventListener: EventTarget["removeEventListener"];
};

type UseWebFileDropOptions = {
    enabled: boolean;
    onFiles: (files: File[]) => void;
    onDragStateChange?: (isDragging: boolean) => void;
};

const hasFiles = (event: DragEvent) => {
    const transfer = event.dataTransfer;
    if (!transfer) return false;
    if (transfer.files.length > 0) return true;
    return Array.from(transfer.types ?? []).includes("Files");
};

const getFiles = (event: DragEvent) =>
    event.dataTransfer?.files ? Array.from(event.dataTransfer.files) : [];

export const useWebFileDrop = ({
    enabled,
    onFiles,
    onDragStateChange,
}: UseWebFileDropOptions) => {
    const [target, setTarget] = useState<FileDropTarget | null>(null);
    const [isDragging, setIsDragging] = useState(false);
    const dragDepthRef = useRef(0);
    const onFilesRef = useRef(onFiles);
    const onDragStateChangeRef = useRef(onDragStateChange);

    useEffect(() => {
        onFilesRef.current = onFiles;
    }, [onFiles]);

    useEffect(() => {
        onDragStateChangeRef.current = onDragStateChange;
    }, [onDragStateChange]);

    const setDragging = useCallback((next: boolean) => {
        setIsDragging(next);
        onDragStateChangeRef.current?.(next);
    }, []);

    const dropRef = useCallback((node: unknown) => {
        setTarget(node as FileDropTarget | null);
    }, []);

    useEffect(() => {
        if (Platform.OS !== "web" || !enabled || !target) {
            dragDepthRef.current = 0;
            setDragging(false);
            return;
        }

        const preventDefault = (event: DragEvent) => {
            if (!hasFiles(event)) return false;
            event.preventDefault();
            event.stopPropagation();
            if (event.dataTransfer) event.dataTransfer.dropEffect = "copy";
            return true;
        };

        const handleDragEnter: EventListener = (event) => {
            const dragEvent = event as DragEvent;
            if (!preventDefault(dragEvent)) return;
            dragDepthRef.current += 1;
            setDragging(true);
        };

        const handleDragOver: EventListener = (event) => {
            preventDefault(event as DragEvent);
        };

        const handleDragLeave: EventListener = (event) => {
            const dragEvent = event as DragEvent;
            if (!preventDefault(dragEvent)) return;
            dragDepthRef.current = Math.max(0, dragDepthRef.current - 1);
            if (dragDepthRef.current === 0) setDragging(false);
        };

        const handleDrop: EventListener = (event) => {
            const dragEvent = event as DragEvent;
            if (!preventDefault(dragEvent)) return;
            dragDepthRef.current = 0;
            setDragging(false);

            const files = getFiles(dragEvent);
            if (files.length > 0) onFilesRef.current(files);
        };

        target.addEventListener("dragenter", handleDragEnter);
        target.addEventListener("dragover", handleDragOver);
        target.addEventListener("dragleave", handleDragLeave);
        target.addEventListener("drop", handleDrop);

        return () => {
            target.removeEventListener("dragenter", handleDragEnter);
            target.removeEventListener("dragover", handleDragOver);
            target.removeEventListener("dragleave", handleDragLeave);
            target.removeEventListener("drop", handleDrop);
            dragDepthRef.current = 0;
            setDragging(false);
        };
    }, [enabled, setDragging, target]);

    return { dropRef, isDragging };
};
