import { useState } from "react";
import * as DocumentPicker from "expo-document-picker";

export const useAttachments = (maxLimit: number, onOpenPicker?: () => void) => {
    const [attachments, setAttachments] = useState<DocumentPicker.DocumentPickerAsset[]>([]);
    const [isFilesModalVisible, setIsFilesModalVisible] = useState(false);

    const handleAttachFiles = async () => {
        if (attachments.length >= maxLimit) {
            alert(`Максимум ${maxLimit} файлов`);
            return;
        }

        try {
            const result = await DocumentPicker.getDocumentAsync({
                copyToCacheDirectory: true,
                multiple: true,
            });

            if (!result.canceled && result.assets.length > 0) {
                setAttachments((prev) => {
                    const combined = [...prev, ...result.assets];
                    return combined.slice(0, maxLimit);
                });
                onOpenPicker?.();
            }
        } catch (error) {
            console.error("Ошибка при выборе файлов:", error);
        }
    };

    const removeAttachment = (index: number) => {
        setAttachments((prev) => {
            const newAttachments = prev.filter((_, i) => i !== index);
            if (newAttachments.length === 0) setIsFilesModalVisible(false);
            return newAttachments;
        });
    };

    const clearAttachments = () => {
        setAttachments([]);
        setIsFilesModalVisible(false);
    };

    return {
        attachments,
        isFilesModalVisible,
        setIsFilesModalVisible,
        handleAttachFiles,
        removeAttachment,
        clearAttachments,
    };
};