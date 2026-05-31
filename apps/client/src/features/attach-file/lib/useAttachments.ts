import { useCallback, useEffect, useRef, useState } from "react";
import type * as DocumentPicker from "expo-document-picker";

type LocalDocumentPickerAsset = DocumentPicker.DocumentPickerAsset & {
    _objectUrlOwner?: true;
};

type AttachmentInput = DocumentPicker.DocumentPickerAsset | File;

type AddAttachmentsOptions = {
    openModal?: boolean;
};

export const CHAT_ATTACHMENT_ALLOWED_MIME_TYPES = [
    "image/jpeg",
    "image/png",
    "image/webp",
    "image/gif",
    "image/heic",
    "image/heif",
    "image/bmp",
    "image/tiff",
    "video/mp4",
    "video/quicktime",
    "video/webm",
    "video/mpeg",
    "video/x-msvideo",
    "video/x-matroska",
    "audio/mpeg",
    "audio/mp4",
    "audio/m4a",
    "audio/x-m4a",
    "audio/ogg",
    "audio/opus",
    "audio/webm",
    "audio/wav",
    "audio/x-wav",
    "audio/aac",
    "audio/flac",
    "application/pdf",
    "application/msword",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "application/vnd.ms-excel",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    "application/vnd.ms-powerpoint",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    "application/vnd.oasis.opendocument.text",
    "application/vnd.oasis.opendocument.spreadsheet",
    "application/vnd.oasis.opendocument.presentation",
    "application/rtf",
    "text/rtf",
    "application/json",
    "application/x-ndjson",
    "application/xml",
    "text/xml",
    "application/yaml",
    "application/x-yaml",
    "text/yaml",
    "text/x-yaml",
    "application/toml",
    "text/x-toml",
    "text/plain",
    "text/csv",
    "text/markdown",
    "text/javascript",
    "application/javascript",
    "application/typescript",
    "text/x-python",
    "text/x-csharp",
    "text/x-java-source",
    "text/x-kotlin",
    "text/x-go",
    "text/x-rust",
    "application/zip",
    "application/x-zip-compressed",
    "application/x-7z-compressed",
    "application/vnd.rar",
    "application/x-rar-compressed",
    "application/x-tar",
    "application/gzip",
    "application/x-gzip",
];

const allowedMimeTypes = new Set(CHAT_ATTACHMENT_ALLOWED_MIME_TYPES);
const blockedMimeTypes = new Set([
    "application/octet-stream",
    "application/x-msdownload",
    "application/x-msdos-program",
    "application/x-bat",
    "application/x-sh",
]);
const allowedExtensions = new Set([
    "jpg", "jpeg", "png", "webp", "gif", "heic", "heif", "bmp", "tif", "tiff",
    "mp4", "mov", "webm", "mpeg", "mpg", "avi", "mkv",
    "mp3", "m4a", "ogg", "opus", "wav", "aac", "flac",
    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "rtf",
    "txt", "csv", "md", "markdown", "json", "jsonl", "xml", "yaml", "yml", "toml",
    "cs", "ts", "tsx", "js", "jsx", "py", "java", "kt", "kts", "go", "rs", "sql",
    "css", "scss", "less", "sh", "ps1", "psm1", "psd1", "sln", "csproj", "props", "targets",
    "zip", "rar", "7z", "tar", "gz", "tgz",
]);

const canCreateObjectUrl = () =>
    typeof URL !== "undefined" && typeof URL.createObjectURL === "function";

const canRevokeObjectUrl = () =>
    typeof URL !== "undefined" && typeof URL.revokeObjectURL === "function";

const isWebFile = (value: AttachmentInput): value is File =>
    typeof File !== "undefined" && value instanceof File;

const getExtension = (name: string | undefined) => {
    const normalized = name?.trim().toLowerCase();
    if (!normalized || !normalized.includes(".")) return "";
    return normalized.slice(normalized.lastIndexOf(".") + 1);
};

export const isAllowedChatAttachment = (attachment: {
    name?: string;
    mimeType?: string;
    type?: string;
}) => {
    const mimeType = (attachment.mimeType || attachment.type || "").trim().toLowerCase();
    if (mimeType && blockedMimeTypes.has(mimeType)) return false;
    if (mimeType && allowedMimeTypes.has(mimeType)) return true;
    return allowedExtensions.has(getExtension(attachment.name));
};

export const filterAllowedAttachments = <T extends { name?: string; mimeType?: string; type?: string }>(
    attachments: T[],
) => attachments.filter(isAllowedChatAttachment);

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
        alert(`ÐœÐ°ÐºÑÐ¸Ð¼ÑƒÐ¼ ${maxLimit} Ñ„Ð°Ð¹Ð»Ð¾Ð²`);
    }, [maxLimit]);

    const alertUnsupported = useCallback(() => {
        alert("Этот тип файла нельзя загрузить в чат");
    }, []);

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

            const allowedAttachments = filterAllowedAttachments(nextAttachments);
            if (allowedAttachments.length !== nextAttachments.length) {
                alertUnsupported();
            }

            const accepted = allowedAttachments
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
        [alertMaxLimit, alertUnsupported, attachments.length, maxLimit, onOpenPicker],
    );

    const handleAttachFiles = async () => {
        if (attachments.length >= maxLimit) {
            alertMaxLimit();
            return;
        }

        try {
            const DocumentPicker = await import("expo-document-picker");
            const result = await DocumentPicker.getDocumentAsync({
                copyToCacheDirectory: true,
                multiple: true,
                type: CHAT_ATTACHMENT_ALLOWED_MIME_TYPES,
            });

            if (!result.canceled && result.assets.length > 0) {
                addAttachments(result.assets);
            }
        } catch (error) {
            console.error("ÐžÑˆÐ¸Ð±ÐºÐ° Ð¿Ñ€Ð¸ Ð²Ñ‹Ð±Ð¾Ñ€Ðµ Ñ„Ð°Ð¹Ð»Ð¾Ð²:", error);
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
