import React, { useEffect, useState, useRef, useCallback } from "react";
import { View, Pressable, Text, TextInput, Keyboard, Animated, Dimensions } from "react-native";
import {
    PaperPlaneRightIcon,
    PencilSimpleIcon,
    PlusCircleIcon,
    SmileyIcon,
    XIcon,
} from "@/shared/ui/phosphor";
import { EmojiPicker } from "@/features/emoji-picker";
import type { DocumentPickerAsset } from "expo-document-picker";

import { useAttachments, FilesModal } from "@/features/attach-file";
import { AttachmentsPreview } from "./AttachmentsPreview";
import { useTypingIndicator } from "@/shared/lib/useTypingIndicator";
import { useComposerStore } from "@/features/message-actions";
import { useChatStore } from "@/entities/conversation/model/chat-store";

const { height: SCREEN_HEIGHT } = Dimensions.get("window");
const MAX_INPUT_HEIGHT = SCREEN_HEIGHT * 0.35;
const MAX_FILES_LIMIT = 10;

interface ChatInputProps {
    conversationId: string;
    onSend: (text: string, files: DocumentPickerAsset[], replyToMessageId?: string) => void;
}

export const ChatInput = ({ conversationId, onSend }: ChatInputProps) => {
    const [query, setQuery] = useState("");
    const { onTextChange: notifyTyping, onSend: notifyStopTyping } = useTypingIndicator(conversationId);
    const [isEmojiVisible, setIsEmojiVisible] = useState(false);
    const [inputHeight, setInputHeight] = useState(24);

    const heightAnim = useRef(new Animated.Value(0)).current;
    const slideAnim = useRef(new Animated.Value(320)).current;

    const replyTo = useComposerStore((s) => s.replyTo);
    const editing = useComposerStore((s) => s.editing);
    const resetComposer = useComposerStore((s) => s.reset);
    const setReply = useComposerStore((s) => s.setReply);
    const editMessage = useChatStore((s) => s.editMessage);

    useEffect(() => {
        if (editing) setQuery(editing.body);
    }, [editing?.id]);

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

    const handleSend = async () => {
        if (!canSend) return;

        const trimmed = query.trim();
        notifyStopTyping();

        if (editing) {
            try {
                await editMessage(editing.id, trimmed);
            } catch (e) {
                console.error("Failed to edit", e);
            }
            setQuery("");
            resetComposer();
            setInputHeight(24);
            return;
        }

        onSend(trimmed, attachments, replyTo?.id);

        setQuery("");
        clearAttachments();
        if (replyTo) setReply(null);
        setInputHeight(24);
    };

    const composerHint = editing
        ? { icon: "edit" as const, title: "Изменение", preview: editing.body }
        : replyTo
            ? { icon: "reply" as const, title: "Ответ", preview: replyTo.body }
            : null;

    const cancelComposerHint = () => {
        if (editing) {
            resetComposer();
            setQuery("");
        } else {
            setReply(null);
        }
    };

    return (
        <View className="border-t border-white/5 bg-app-card p-4">
            {composerHint && (
                <View className="flex-row items-start gap-2 mb-2 pl-2 border-l-2 border-brand-400">
                    {composerHint.icon === "edit" ? (
                        <PencilSimpleIcon size={14} className="text-brand-400 mt-0.5" />
                    ) : null}
                    <View className="flex-1 min-w-0">
                        <Text className="text-brand-400 text-xs font-semibold">
                            {composerHint.title}
                        </Text>
                        <Text
                            className="text-text-subtle text-xs"
                            numberOfLines={1}
                        >
                            {composerHint.preview}
                        </Text>
                    </View>
                    <Pressable onPress={cancelComposerHint} hitSlop={8} className="p-1">
                        <XIcon size={14} className="text-text-muted" />
                    </Pressable>
                </View>
            )}

            <AttachmentsPreview
                attachments={attachments}
                onRemove={removeAttachment}
                onOpenModal={() => setIsFilesModalVisible(true)}
            />

            <View className="flex-row items-end gap-3">
                <Pressable
                    onPress={handleAttachFiles}
                    className={`active:opacity-60 mb-2 ${attachments.length >= MAX_FILES_LIMIT || !!editing ? "opacity-30" : ""}`}
                    disabled={attachments.length >= MAX_FILES_LIMIT || !!editing}
                >
                    <PlusCircleIcon size={28} className="text-text-subtle" />
                </Pressable>

                <View className="flex-1 flex-row items-end bg-white/5 rounded-2xl px-4">
                    <TextInput
                        className="text-white flex-1 text-[15px]"
                        placeholder={editing ? "Изменение сообщения" : "Сообщение"}
                        placeholderTextColor="#8B8FA8"
                        value={query}
                        onChangeText={(text) => {
                            setQuery(text);
                            if (!editing) notifyTyping(text);
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
