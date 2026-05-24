import React, { useEffect, useState } from "react";
import { Pressable, Text, TextInput, View } from "react-native";
import { ModalOverlay } from "@/shared/ui";
import { useChatStore } from "@/entities/conversation/model/chat-store";
import { useComposerStore } from "../model/composer-store";

export const EditMessageModal = () => {
    const editing = useComposerStore((s) => s.editing);
    const setEditing = useComposerStore((s) => s.setEditing);
    const editMessage = useChatStore((s) => s.editMessage);
    const [body, setBody] = useState("");
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        setBody(editing?.body ?? "");
    }, [editing?.id]);

    if (!editing) return null;

    const onClose = () => setEditing(null);

    const onSave = async () => {
        const trimmed = body.trim();
        if (!trimmed || trimmed === editing.body) {
            onClose();
            return;
        }
        setSaving(true);
        try {
            await editMessage(editing.id, trimmed);
        } catch (e) {
            console.error("Failed to edit message", e);
        } finally {
            setSaving(false);
            onClose();
        }
    };

    return (
        <ModalOverlay
            visible={!!editing}
            onClose={onClose}
            contentClassName="bg-app-card border border-white/10 rounded-2xl w-[420px] max-w-[90%] p-4"
        >
            <Text className="text-white text-base font-semibold mb-3">
                Изменить сообщение
            </Text>
            <TextInput
                value={body}
                onChangeText={setBody}
                multiline
                autoFocus
                className="bg-white/5 rounded-xl px-3 py-2 text-white text-[15px] min-h-[80px]"
                style={{ textAlignVertical: "top" }}
            />
            <View className="flex-row justify-end gap-2 mt-4">
                <Pressable
                    onPress={onClose}
                    className="px-4 py-2 rounded-xl active:bg-white/10"
                >
                    <Text className="text-text-subtle font-medium">Отмена</Text>
                </Pressable>
                <Pressable
                    onPress={onSave}
                    disabled={saving}
                    className={`px-4 py-2 rounded-xl ${saving ? "bg-brand-600/40" : "bg-brand-600 active:opacity-80"}`}
                >
                    <Text className="text-white font-medium">Сохранить</Text>
                </Pressable>
            </View>
        </ModalOverlay>
    );
};
