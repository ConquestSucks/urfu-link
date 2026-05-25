import React, { useCallback, useMemo } from "react";
import {
    Modal,
    Pressable,
    ScrollView,
    StyleSheet,
    Text,
    View,
} from "react-native";
import * as DocumentPicker from "expo-document-picker";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import { useWebFileDrop } from "@/shared/lib/useWebFileDrop";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import {
    FileIcon,
    ImageSquareIcon,
    UploadSimpleIcon,
    XIcon,
} from "@/shared/ui/phosphor";

interface FilesModalProps {
    visible: boolean;
    onClose: () => void;
    attachments: DocumentPicker.DocumentPickerAsset[];
    onPickFiles: () => void;
    onAddDroppedFiles: (files: File[]) => void;
    onRemove: (index: number) => void;
    onSubmit: () => void | Promise<void>;
    isSubmitting?: boolean;
    submitError?: string | null;
}

const formatFileSize = (size?: number) => {
    if (!size || size <= 0) return "0 KB";
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    return `${(size / 1024 / 1024).toFixed(1)} MB`;
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
    submitError = null,
}: FilesModalProps) => {
    const { width, isMobile } = useWindowSize();
    const canSubmit = attachments.length > 0 && !isSubmitting;
    const { dropRef, isDragging } = useWebFileDrop({
        enabled: visible && !isSubmitting,
        onFiles: onAddDroppedFiles,
    });
    const panelWidth = useMemo(() => {
        if (isMobile) return width;
        return Math.min(Math.max(width - 32, 320), 660);
    }, [isMobile, width]);
    const handleClose = useCallback(() => {
        if (isSubmitting) return;
        onClose();
    }, [isSubmitting, onClose]);

    return (
        <Modal visible={visible} animationType="fade" transparent onRequestClose={handleClose}>
            <View className="flex-1 justify-center items-center bg-black/70">
                <View
                    testID="files-modal-panel"
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
                            onPress={handleClose}
                            disabled={isSubmitting}
                            hitSlop={10}
                            className={`p-1 ${isSubmitting ? "opacity-40" : "active:opacity-60"}`}
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
                            ref={dropRef}
                            testID="files-modal-drop-zone"
                            onPress={onPickFiles}
                            disabled={isSubmitting}
                            className={`items-center justify-center ${isSubmitting ? "opacity-70" : "active:opacity-90"}`}
                            style={[
                                styles.dropZone,
                                isDragging ? styles.dropZoneActive : null,
                            ]}
                        >
                            <View className="items-center justify-center mb-4" style={styles.uploadIconTile}>
                                <UploadSimpleIcon size={26} className="text-text-muted" />
                            </View>
                            <Text className="text-white text-[18px] font-semibold text-center">
                                Перетащите файлы сюда
                            </Text>
                            <Text className="text-text-placeholder text-[14px] mt-1 text-center">
                                или нажмите для выбора файлов
                            </Text>
                        </Pressable>

                        <View className="flex-row items-center justify-between mt-5 mb-3">
                            <Text className="text-text-muted text-[15px] font-semibold">
                                Выбрано файлов: {attachments.length}
                            </Text>
                            {isSubmitting ? (
                                <Text className="text-brand-400 text-[13px] font-medium">
                                    Загрузка файлов и отправка сообщения...
                                </Text>
                            ) : null}
                        </View>

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
                                            <Icon size={20} className="text-brand-400" />
                                        </View>
                                        <View className="flex-1 min-w-0">
                                            <Text
                                                className="text-white text-[15px] font-semibold"
                                                numberOfLines={1}
                                            >
                                                {file.name}
                                            </Text>
                                            <Text className="text-text-muted text-[13px] mt-0.5">
                                                {formatFileSize(file.size)}
                                            </Text>
                                        </View>
                                        <Pressable
                                            testID={`files-modal-remove-${index}`}
                                            onPress={() => onRemove(index)}
                                            disabled={isSubmitting}
                                            hitSlop={10}
                                            className={`p-2 ${isSubmitting ? "opacity-35" : "active:opacity-60"}`}
                                        >
                                            <XIcon size={18} className="text-text-muted" />
                                        </Pressable>
                                    </View>
                                );
                            })}
                        </View>

                        {submitError ? (
                            <Text className="text-danger-300 text-[13px] font-medium mt-4">
                                {submitError}
                            </Text>
                        ) : null}
                    </ScrollView>

                    <View
                        className="flex-row items-center justify-end border-t border-white/10"
                        style={styles.footer}
                    >
                        <Pressable
                            onPress={handleClose}
                            disabled={isSubmitting}
                            className={`px-5 py-2.5 mr-3 ${isSubmitting ? "opacity-40" : "active:opacity-60"}`}
                        >
                            <Text className="text-text-muted text-[15px] font-semibold">
                                Отмена
                            </Text>
                        </Pressable>
                        <Pressable
                            testID="files-modal-submit"
                            onPress={onSubmit}
                            disabled={!canSubmit}
                            className={`items-center justify-center ${isSubmitting ? "bg-brand-650/70" : canSubmit ? "bg-brand-650 active:opacity-85" : "bg-brand-650/40"}`}
                            style={styles.submitButton}
                        >
                            {isSubmitting ? (
                                <View className="flex-row items-center">
                                    <ActivityIndicator size="small" className="text-white" />
                                    <Text className="text-white text-[15px] font-semibold ml-2">
                                        Отправка...
                                    </Text>
                                </View>
                            ) : (
                                <Text className="text-white text-[15px] font-semibold">
                                    Отправить ({attachments.length})
                                </Text>
                            )}
                        </Pressable>
                    </View>
                </View>
            </View>
        </Modal>
    );
};

const styles = StyleSheet.create({
    panel: {
        borderRadius: 22,
        maxHeight: "80%",
    },
    mobilePanel: {
        borderRadius: 0,
        height: "100%",
        maxHeight: "100%",
    },
    header: {
        minHeight: 72,
        paddingHorizontal: 24,
        paddingVertical: 18,
    },
    content: {
        paddingHorizontal: 24,
        paddingTop: 20,
        paddingBottom: 20,
    },
    dropZone: {
        minHeight: 164,
        borderWidth: 2,
        borderColor: "#34415D",
        borderRadius: 16,
        backgroundColor: "#172033",
    },
    dropZoneActive: {
        borderColor: "#51A2FF",
        backgroundColor: "rgba(43, 127, 255, 0.12)",
    },
    uploadIconTile: {
        width: 54,
        height: 54,
        borderRadius: 14,
        backgroundColor: "#283147",
    },
    fileList: {
        gap: 12,
    },
    fileRow: {
        minHeight: 64,
        borderRadius: 12,
        paddingHorizontal: 12,
        backgroundColor: "#172033",
    },
    fileIconTile: {
        width: 40,
        height: 40,
        borderRadius: 8,
        backgroundColor: "#202A40",
    },
    footer: {
        minHeight: 72,
        paddingHorizontal: 24,
        paddingVertical: 12,
        backgroundColor: "#151E31",
    },
    submitButton: {
        minWidth: 156,
        height: 46,
        borderRadius: 12,
    },
});
