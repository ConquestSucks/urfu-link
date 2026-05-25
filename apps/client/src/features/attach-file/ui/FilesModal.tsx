import React, { useCallback, useMemo } from "react";
import {
    Modal,
    Platform,
    Pressable,
    ScrollView,
    StyleSheet,
    Text,
    View,
} from "react-native";
import * as DocumentPicker from "expo-document-picker";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import {
    FileIcon,
    ImageSquareIcon,
    UploadSimpleIcon,
    XIcon,
} from "@/shared/ui/phosphor";

type WebDragEvent = {
    preventDefault?: () => void;
    stopPropagation?: () => void;
    dataTransfer?: DataTransfer;
    nativeEvent?: {
        dataTransfer?: DataTransfer;
    };
};

interface FilesModalProps {
    visible: boolean;
    onClose: () => void;
    attachments: DocumentPicker.DocumentPickerAsset[];
    onPickFiles: () => void;
    onAddDroppedFiles: (files: File[]) => void;
    onRemove: (index: number) => void;
    onSubmit: () => void | Promise<void>;
    isSubmitting?: boolean;
}

const formatFileSize = (size?: number) => {
    if (!size || size <= 0) return "0 KB";
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    return `${(size / 1024 / 1024).toFixed(1)} MB`;
};

const getTransferFiles = (event: WebDragEvent) => {
    const transfer = event.nativeEvent?.dataTransfer ?? event.dataTransfer;
    return transfer?.files ? Array.from(transfer.files) : [];
};

export const FilesModal = ({
    visible,
    onClose,
    attachments,
    onPickFiles,
    onAddDroppedFiles,
    onRemove,
    onSubmit,
    isSubmitting = false,
}: FilesModalProps) => {
    const { width, isMobile } = useWindowSize();
    const canSubmit = attachments.length > 0 && !isSubmitting;
    const panelWidth = useMemo(() => {
        if (isMobile) return width;
        return Math.min(Math.max(width - 20, 320), 878);
    }, [isMobile, width]);

    const preventDefaultDrag = useCallback((event: WebDragEvent) => {
        event.preventDefault?.();
        event.stopPropagation?.();
    }, []);

    const handleDrop = useCallback(
        (event: WebDragEvent) => {
            preventDefaultDrag(event);
            const files = getTransferFiles(event);
            if (files.length > 0) onAddDroppedFiles(files);
        },
        [onAddDroppedFiles, preventDefaultDrag],
    );

    const dropZoneWebProps =
        Platform.OS === "web"
            ? ({
                  onDragEnter: preventDefaultDrag,
                  onDragOver: preventDefaultDrag,
                  onDrop: handleDrop,
              } as Record<string, unknown>)
            : {};

    return (
        <Modal visible={visible} animationType="fade" transparent onRequestClose={onClose}>
            <View className="flex-1 justify-center items-center bg-black/70">
                <View
                    className="bg-app-card overflow-hidden border border-white/10"
                    style={[
                        styles.panel,
                        isMobile ? styles.mobilePanel : null,
                        { width: panelWidth },
                    ]}
                >
                    <View
                        className="flex-row items-center justify-between border-b border-white/10"
                        style={styles.header}
                    >
                        <Text className="text-white text-[24px] font-bold">
                            Загрузить файлы
                        </Text>
                        <Pressable
                            onPress={onClose}
                            hitSlop={10}
                            className="active:opacity-60 p-1"
                        >
                            <XIcon size={26} className="text-text-muted" />
                        </Pressable>
                    </View>

                    <ScrollView
                        className="flex-1"
                        contentContainerStyle={styles.content}
                        keyboardShouldPersistTaps="handled"
                    >
                        <Pressable
                            testID="files-modal-drop-zone"
                            onPress={onPickFiles}
                            className="items-center justify-center active:opacity-90"
                            style={styles.dropZone}
                            {...dropZoneWebProps}
                        >
                            <View className="items-center justify-center mb-6" style={styles.uploadIconTile}>
                                <UploadSimpleIcon size={38} className="text-text-muted" />
                            </View>
                            <Text className="text-white text-[23px] font-semibold text-center">
                                Перетащите файлы сюда
                            </Text>
                            <Text className="text-text-placeholder text-[18px] mt-2 text-center">
                                или нажмите для выбора файлов
                            </Text>
                        </Pressable>

                        <Text className="text-text-muted text-[18px] font-semibold mt-8 mb-4">
                            Выбрано файлов: {attachments.length}
                        </Text>

                        <View style={styles.fileList}>
                            {attachments.map((file, index) => {
                                const isImage = file.mimeType?.startsWith("image/");
                                const Icon = isImage ? ImageSquareIcon : FileIcon;

                                return (
                                    <View
                                        key={`${file.uri}-${index}`}
                                        className="flex-row items-center border border-white/5"
                                        style={styles.fileRow}
                                    >
                                        <View
                                            className="items-center justify-center mr-4"
                                            style={styles.fileIconTile}
                                        >
                                            <Icon size={24} className="text-brand-400" />
                                        </View>
                                        <View className="flex-1 min-w-0">
                                            <Text
                                                className="text-white text-[18px] font-semibold"
                                                numberOfLines={1}
                                            >
                                                {file.name}
                                            </Text>
                                            <Text className="text-text-muted text-[15px] mt-1">
                                                {formatFileSize(file.size)}
                                            </Text>
                                        </View>
                                        <Pressable
                                            testID={`files-modal-remove-${index}`}
                                            onPress={() => onRemove(index)}
                                            hitSlop={10}
                                            className="active:opacity-60 p-2"
                                        >
                                            <XIcon size={22} className="text-text-muted" />
                                        </Pressable>
                                    </View>
                                );
                            })}
                        </View>
                    </ScrollView>

                    <View
                        className="flex-row items-center justify-end border-t border-white/10"
                        style={styles.footer}
                    >
                        <Pressable
                            onPress={onClose}
                            className="active:opacity-60 px-6 py-3 mr-4"
                        >
                            <Text className="text-text-muted text-[18px] font-semibold">
                                Отмена
                            </Text>
                        </Pressable>
                        <Pressable
                            testID="files-modal-submit"
                            onPress={onSubmit}
                            disabled={!canSubmit}
                            className={`items-center justify-center ${canSubmit ? "bg-brand-650 active:opacity-85" : "bg-brand-650/40"}`}
                            style={styles.submitButton}
                        >
                            <Text className="text-white text-[18px] font-semibold">
                                Отправить ({attachments.length})
                            </Text>
                        </Pressable>
                    </View>
                </View>
            </View>
        </Modal>
    );
};

const styles = StyleSheet.create({
    panel: {
        borderRadius: 30,
        maxHeight: "92%",
    },
    mobilePanel: {
        borderRadius: 0,
        height: "100%",
        maxHeight: "100%",
    },
    header: {
        minHeight: 100,
        paddingHorizontal: 32,
        paddingVertical: 22,
    },
    content: {
        paddingHorizontal: 32,
        paddingTop: 32,
        paddingBottom: 32,
    },
    dropZone: {
        minHeight: 298,
        borderWidth: 2,
        borderColor: "#34415D",
        borderRadius: 20,
        backgroundColor: "#172033",
    },
    uploadIconTile: {
        width: 84,
        height: 84,
        borderRadius: 20,
        backgroundColor: "#283147",
    },
    fileList: {
        gap: 12,
    },
    fileRow: {
        minHeight: 86,
        borderRadius: 14,
        paddingHorizontal: 16,
        backgroundColor: "#172033",
    },
    fileIconTile: {
        width: 52,
        height: 52,
        borderRadius: 10,
        backgroundColor: "#202A40",
    },
    footer: {
        minHeight: 90,
        paddingHorizontal: 32,
        paddingVertical: 16,
        backgroundColor: "#151E31",
    },
    submitButton: {
        minWidth: 186,
        height: 56,
        borderRadius: 14,
    },
});
