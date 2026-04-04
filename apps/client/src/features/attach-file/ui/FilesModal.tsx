import React from "react";
import { View, Text, Pressable, ScrollView, Modal } from "react-native";
import { CaretLeftIcon, XIcon, FileIcon } from "@/shared/ui/phosphor";
import { useWindowSize } from "@/shared/lib/useWindowSize";
import * as DocumentPicker from "expo-document-picker";

interface FilesModalProps {
    visible: boolean;
    onClose: () => void;
    attachments: DocumentPicker.DocumentPickerAsset[];
    onRemove: (index: number) => void;
}

export const FilesModal = ({ visible, onClose, attachments, onRemove }: FilesModalProps) => {
    const { isMobile } = useWindowSize();

    return (
        <Modal visible={visible} animationType="fade" transparent={true} onRequestClose={onClose}>
            <View className="flex-1 justify-center items-center bg-black/50">
                <View
                    className={`bg-app-bg overflow-hidden ${
                        isMobile ? "w-full h-full" : "w-1/2 h-2/3 rounded-2xl"
                    }`}
                >
                    <View className="flex-row items-center justify-between py-3.5 px-6 border-b border-white/10">
                        {isMobile && (
                            <View className="flex-row items-center gap-6 flex-1">
                                <Pressable onPress={onClose} className="active:opacity-60 p-1">
                                    <CaretLeftIcon size={20} className="text-white" />
                                </Pressable>
                                <Text className="text-white text-lg font-bold">
                                    Прикрепленные файлы ({attachments.length})
                                </Text>
                            </View>
                        )}
                        {!isMobile && (
                            <View className="flex-row items-center justify-between gap-3 flex-1">
                                <Text className="text-white text-lg font-bold">
                                    Прикрепленные файлы ({attachments.length})
                                </Text>
                                <Pressable onPress={onClose} className="active:opacity-60 p-1">
                                    <XIcon size={20} className="text-white" />
                                </Pressable>
                            </View>
                        )}
                    </View>

                    <ScrollView className="flex-1 p-4">
                        {attachments.map((file, index) => (
                            <View
                                key={`${file.uri}-${index}`}
                                className="flex-row items-center bg-white/5 p-4 rounded-xl mb-3"
                            >
                                <FileIcon size={24} className="text-brand-400 mr-3" />
                                <View className="flex-1">
                                    <Text
                                        className="text-white text-sm font-medium mb-1"
                                        numberOfLines={1}
                                    >
                                        {file.name}
                                    </Text>
                                    <Text className="text-text-subtle text-xs">
                                        {file.size ? (file.size / 1024 / 1024).toFixed(2) : 0} MB
                                    </Text>
                                </View>
                                <Pressable
                                    onPress={() => onRemove(index)}
                                    className="p-2 bg-white/5 rounded-full active:bg-white/10"
                                >
                                    <XIcon size={16} className="text-text-muted hover:text-white" />
                                </Pressable>
                            </View>
                        ))}
                    </ScrollView>
                </View>
            </View>
        </Modal>
    );
};
