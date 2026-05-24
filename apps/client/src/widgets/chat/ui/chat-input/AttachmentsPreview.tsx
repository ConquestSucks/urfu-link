import React from "react";
import { View, Text, Pressable } from "react-native";
import { FileIcon, XIcon } from "@/shared/ui/phosphor";
import * as DocumentPicker from "expo-document-picker";

interface Props {
    attachments: DocumentPicker.DocumentPickerAsset[];
    onRemove: (index: number) => void;
    onOpenModal: () => void;
}

export const AttachmentsPreview = ({ attachments, onRemove, onOpenModal }: Props) => {
    if (attachments.length === 0) return null;

    return (
        <View className="mb-3 flex-row gap-2">
            {attachments.length <= 2 ? (
                attachments.map((file, index) => (
                    <View key={`${file.uri}-${index}`} className="flex-row items-center bg-white/10 pl-3 pr-2 py-2 rounded-xl gap-2 max-w-[50%]">
                        <FileIcon size={16} className="text-brand-400" />
                        <Text className="text-white text-xs font-medium flex-1" numberOfLines={1}>
                            {file.name}
                        </Text>
                        <Pressable onPress={() => onRemove(index)} className="p-1 active:opacity-60">
                            <XIcon size={14} className="text-text-subtle" />
                        </Pressable>
                    </View>
                ))
            ) : (
                <Pressable onPress={onOpenModal} className="flex-row items-center bg-white/10 px-4 py-2 rounded-xl gap-2 active:opacity-80">
                    <FileIcon size={16} className="text-brand-400" />
                    <Text className="text-white text-xs font-medium">
                        Прикреплено {attachments.length} файлов
                    </Text>
                </Pressable>
            )}
        </View>
    );
};