import React, { useState, useRef, useCallback } from "react";
import { View, Pressable, TextInput, Keyboard, Animated, Dimensions } from "react-native";
import { PaperPlaneRightIcon, PlusCircleIcon, SmileyIcon } from "@/shared/ui/phosphor";
import { EmojiPicker } from "@/features/emoji-picker";
import type { DocumentPickerAsset } from "expo-document-picker";

import { useAttachments, FilesModal } from "@/features/attach-file";
import { AttachmentsPreview } from "./AttachmentsPreview";

const { height: SCREEN_HEIGHT } = Dimensions.get("window");
const MAX_INPUT_HEIGHT = SCREEN_HEIGHT * 0.35;
const MAX_FILES_LIMIT = 10;

interface ChatInputProps {
    onSend: (text: string, files: DocumentPickerAsset[]) => void;
}

export const ChatInput = ({ onSend }: ChatInputProps) => {
    const [query, setQuery] = useState("");
    const [isEmojiVisible, setIsEmojiVisible] = useState(false);
    const [inputHeight, setInputHeight] = useState(24);

    const heightAnim = useRef(new Animated.Value(0)).current;
    const slideAnim = useRef(new Animated.Value(320)).current;

    const animate = useCallback(
        (show: boolean) => {
            Animated.parallel([
                Animated.timing(heightAnim, {
                    toValue: show ? 320 : 0,
                    duration: 250,
                    useNativeDriver: false,
                }),
                Animated.timing(slideAnim, {
                    toValue: show ? 0 : 320,
                    duration: 250,
                    useNativeDriver: true,
                }),
            ]).start(() => {
                if (!show) setIsEmojiVisible(false);
            });
        },
        [heightAnim, slideAnim],
    );

    const {
        attachments,
        isFilesModalVisible,
        setIsFilesModalVisible,
        handleAttachFiles,
        removeAttachment,
        clearAttachments,
    } = useAttachments(MAX_FILES_LIMIT, () => animate(false));

    const canSend = query.trim().length > 0 || attachments.length > 0;

    const handlePickEmoji = useCallback((emoji: string) => setQuery((prev) => prev + emoji), []);

    const handleSend = () => {
        if (!canSend) return;

        onSend(query.trim(), attachments);

        setQuery("");
        clearAttachments();
        setInputHeight(24);
    };

    return (
        <View className="border-t border-white/5 bg-app-card p-4">
            <AttachmentsPreview
                attachments={attachments}
                onRemove={removeAttachment}
                onOpenModal={() => setIsFilesModalVisible(true)}
            />

            <View className="flex-row items-end gap-3">
                <Pressable
                    onPress={handleAttachFiles}
                    className={`active:opacity-60 mb-2 ${attachments.length >= MAX_FILES_LIMIT ? "opacity-30" : ""}`}
                    disabled={attachments.length >= MAX_FILES_LIMIT}
                >
                    <PlusCircleIcon size={28} className="text-text-subtle" />
                </Pressable>

                <View className="flex-1 flex-row items-end bg-white/5 rounded-2xl px-4">
                    <TextInput
                        className="text-white flex-1 text-[15px]"
                        placeholder="Сообщение"
                        placeholderTextColor="#8B8FA8"
                        value={query}
                        onChangeText={(text) => {
                            setQuery(text);
                            if (text === "") setInputHeight(24);
                        }}
                        onFocus={() => isEmojiVisible && animate(false)}
                        multiline
                        onContentSizeChange={(e) =>
                            setInputHeight(e.nativeEvent.contentSize.height)
                        }
                        style={{
                            height: Math.max(24, Math.min(inputHeight, MAX_INPUT_HEIGHT)),
                            textAlignVertical: "center",
                            paddingTop: 12,
                            paddingBottom: 12,
                        }}
                    />
                    <Pressable
                        onPress={() => {
                            Keyboard.dismiss();
                            const willShow = !isEmojiVisible;
                            setIsEmojiVisible(willShow);
                            animate(willShow);
                        }}
                        className="py-2.5 ml-2"
                    >
                        <SmileyIcon
                            size={24}
                            className={isEmojiVisible ? "text-brand-600" : "text-text-subtle"}
                            weight={isEmojiVisible ? "fill" : "regular"}
                        />
                    </Pressable>
                </View>

                <Pressable
                    onPress={handleSend}
                    disabled={!canSend}
                    className={`w-11 h-11 rounded-full items-center justify-center ${canSend ? "bg-brand-600 active:opacity-80" : "bg-brand-600/30"}`}
                >
                    <PaperPlaneRightIcon
                        size={22}
                        className={canSend ? "text-white" : "text-white/40"}
                        weight="fill"
                    />
                </Pressable>
            </View>

            <Animated.View
                style={{ height: heightAnim, overflow: "hidden", backgroundColor: "#0B1225" }}
            >
                <Animated.View
                    style={{ height: 320, width: "100%", transform: [{ translateY: slideAnim }] }}
                >
                    <EmojiPicker onPick={handlePickEmoji} />
                </Animated.View>
            </Animated.View>

            <FilesModal
                visible={isFilesModalVisible}
                onClose={() => setIsFilesModalVisible(false)}
                attachments={attachments}
                onRemove={removeAttachment}
            />
        </View>
    );
};
