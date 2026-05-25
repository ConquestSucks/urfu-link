import { useCallback, useEffect, useRef, useState } from "react";
import * as DocumentPicker from "expo-document-picker";

type LocalDocumentPickerAsset = DocumentPicker.DocumentPickerAsset & {
    _objectUrlOwner?: true;
};

type AttachmentInput = DocumentPicker.DocumentPickerAsset | File;

type AddAttachmentsOptions = {
    openModal?: boolean;
};

const canCreateObjectUrl = () =>
    typeof URL !== "undefined" && typeof URL.createObjectURL === "function";

const canRevokeObjectUrl = () =>
    typeof URL !== "undefined" && typeof URL.revokeObjectURL === "function";

const isWebFile = (value: AttachmentInput): value is File =>
    typeof File !== "undefined" && value instanceof File;

const revokeObjectUrl = (asset: DocumentPicker.DocumentPickerAsset) => {
    const localAsset = asset as LocalDocumentPickerAsset;
    if (!localAsset._objectUrlOwner || !canRevokeObjectUrl()) return;
    URL.revokeObjectURL(asset.uri);
};

export const normalizeDroppedFile = (file: File): DocumentPicker.DocumentPickerAsset => {
    const uri = canCreateObjectUrl()
        ? URL.createObjectURL(file)
        : `dropped-file://${encodeURIComponent(file.name)}`;

    return {
        name: file.name || "file",
        size: file.size,
        uri,
        mimeType: file.type || undefined,
        lastModified: file.lastModified,
        file,
        _objectUrlOwner: uri.startsWith("blob:") ? true : undefined,
    } as LocalDocumentPickerAsset;
};

export const normalizeDroppedFiles = (files: File[]) => files.map(normalizeDroppedFile);

export const useAttachments = (maxLimit: number, onOpenPicker?: () => void) => {
    const [attachments, setAttachments] = useState<DocumentPicker.DocumentPickerAsset[]>([]);
    const [isFilesModalVisible, setIsFilesModalVisible] = useState(false);
    const attachmentsRef = useRef<DocumentPicker.DocumentPickerAsset[]>([]);

    useEffect(() => {
        attachmentsRef.current = attachments;
    }, [attachments]);

    useEffect(
        () => () => {
            attachmentsRef.current.forEach(revokeObjectUrl);
        },
        [],
    );

    const alertMaxLimit = useCallback(() => {
        alert(`Максимум ${maxLimit} файлов`);
    }, [maxLimit]);

    const addAttachments = useCallback(
        (nextAttachments: AttachmentInput[], options: AddAttachmentsOptions = {}) => {
            if (nextAttachments.length === 0) return;

            const remaining = Math.max(maxLimit - attachments.length, 0);
            if (remaining <= 0) {
                alertMaxLimit();
                if (options.openModal) setIsFilesModalVisible(true);
                return;
            }

            if (nextAttachments.length > remaining) {
                alertMaxLimit();
            }

            const accepted = nextAttachments
                .slice(0, remaining)
                .map((attachment) =>
                    isWebFile(attachment) ? normalizeDroppedFile(attachment) : attachment,
                );

            setAttachments((prev) => {
                const liveRemaining = Math.max(maxLimit - prev.length, 0);
                const liveAccepted = accepted.slice(0, liveRemaining);
                accepted.slice(liveRemaining).forEach(revokeObjectUrl);
                return [...prev, ...liveAccepted];
            });

            onOpenPicker?.();
            if (options.openModal) setIsFilesModalVisible(true);
        },
        [alertMaxLimit, attachments.length, maxLimit, onOpenPicker],
    );

    const handleAttachFiles = async () => {
        if (attachments.length >= maxLimit) {
            alertMaxLimit();
            return;
        }

        try {
            const result = await DocumentPicker.getDocumentAsync({
                copyToCacheDirectory: true,
                multiple: true,
            });

            if (!result.canceled && result.assets.length > 0) {
                addAttachments(result.assets);
            }
        } catch (error) {
            console.error("Ошибка при выборе файлов:", error);
        }
    };

    const removeAttachment = (index: number) => {
        setAttachments((prev) => {
            const removed = prev[index];
            if (removed) revokeObjectUrl(removed);
            const newAttachments = prev.filter((_, i) => i !== index);
            return newAttachments;
        });
    };

    const clearAttachments = () => {
        attachments.forEach(revokeObjectUrl);
        setAttachments([]);
        setIsFilesModalVisible(false);
    };

    return {
        attachments,
        isFilesModalVisible,
        setIsFilesModalVisible,
        addAttachments,
        handleAttachFiles,
        removeAttachment,
        clearAttachments,
    };
};
