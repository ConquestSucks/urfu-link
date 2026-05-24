import React, { useEffect, useMemo, useState, useRef, useCallback } from "react";
import { View, Pressable, Text, TextInput, Keyboard, Animated, Dimensions, Platform } from "react-native";
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
import { useParticipantsStore, useConversationParticipants } from "@/entities/conversation/model/participants-store";
import { findMentionAtCursor, MentionSuggestions } from "@/features/mentions";
import { useCurrentUserId } from "@/shared/store/auth-store";

const { height: SCREEN_HEIGHT } = Dimensions.get("window");
const MIN_INPUT_CONTENT_HEIGHT = 24;
const INPUT_LINE_HEIGHT = 24;
const INPUT_VERTICAL_PADDING = 10;
const MAX_INPUT_CONTENT_HEIGHT = SCREEN_HEIGHT * 0.35;
const MAX_FILES_LIMIT = 10;
const WEB_STALE_MEASUREMENT_THRESHOLD = INPUT_LINE_HEIGHT * 4;

const getComposerInputHeight = (contentHeight: number) =>
    Math.max(MIN_INPUT_CONTENT_HEIGHT, Math.min(contentHeight, MAX_INPUT_CONTENT_HEIGHT)) +
    INPUT_VERTICAL_PADDING * 2;

const getExplicitLineCount = (text: string) => text.split("\n").length;

const getExplicitLineContentHeight = (text: string) =>
    Math.max(
        MIN_INPUT_CONTENT_HEIGHT,
        getExplicitLineCount(text) * INPUT_LINE_HEIGHT +
            (MIN_INPUT_CONTENT_HEIGHT - INPUT_LINE_HEIGHT),
    );

const normalizeMeasuredContentHeight = (measuredHeight: number, text: string) => {
    if (text.length === 0) return MIN_INPUT_CONTENT_HEIGHT;
    if (Platform.OS !== "web") return measuredHeight;

    const measuredWithoutPadding = Math.max(
        MIN_INPUT_CONTENT_HEIGHT,
        measuredHeight - INPUT_VERTICAL_PADDING * 2,
    );
    const explicitLineHeight = getExplicitLineContentHeight(text);

    if (
        text.length < 200 &&
        measuredWithoutPadding > explicitLineHeight + WEB_STALE_MEASUREMENT_THRESHOLD
    ) {
        return explicitLineHeight;
    }

    return Math.max(measuredWithoutPadding, explicitLineHeight);
};

interface ChatInputProps {
    conversationId: string;
    onSend: (text: string, files: DocumentPickerAsset[], replyToMessageId?: string) => void;
    typingEnabled?: boolean;
}

export const ChatInput = ({ conversationId, onSend, typingEnabled = true }: ChatInputProps) => {
    const [query, setQuery] = useState("");
    const { onTextChange: notifyTyping, onSend: notifyStopTyping } = useTypingIndicator(
        conversationId,
        { enabled: typingEnabled },
    );
    const [isEmojiVisible, setIsEmojiVisible] = useState(false);
    const [inputHeight, setInputHeight] = useState(MIN_INPUT_CONTENT_HEIGHT);
    // Курсор: позиция точки вставки в тексте. Нужен для детекта @-токена.
    const [selection, setSelection] = useState<{ start: number; end: number }>({ start: 0, end: 0 });
    // Программно проставляемая selection после вставки @mention. RN сбрасывает
    // её обратно в "контролируемый" режим до следующего пользовательского ввода.
    const [pendingSelection, setPendingSelection] = useState<{ start: number; end: number } | null>(null);
    const inputRef = useRef<TextInput>(null);
    const previousLineCountRef = useRef(1);
    const pendingLineChangeContentHeightRef = useRef<number | null>(null);
    const pendingLineChangeDirectionRef = useRef<"grow" | "shrink" | null>(null);

    const heightAnim = useRef(new Animated.Value(0)).current;
    const slideAnim = useRef(new Animated.Value(320)).current;

    const replyTo = useComposerStore((s) => s.replyTo);
    const editing = useComposerStore((s) => s.editing);
    const resetComposer = useComposerStore((s) => s.reset);
    const setReply = useComposerStore((s) => s.setReply);
    const editMessage = useChatStore((s) => s.editMessage);
    const currentUserId = useCurrentUserId();
    const participants = useConversationParticipants(conversationId);

    // Токен под курсором: либо валидный mention, либо null.
    const mentionToken = useMemo(() => findMentionAtCursor(query, selection.start), [
        query, selection.start,
    ]);

    // Фильтр participants по подстроке displayName. Сами себя не предлагаем.
    const mentionSuggestions = useMemo(() => {
        if (!mentionToken) return [];
        const q = mentionToken.query.toLowerCase();
        return participants
            .filter((p) => p.userId !== currentUserId)
            .filter((p) => !q || (p.displayName || "").toLowerCase().includes(q))
            .slice(0, 8);
    }, [mentionToken, participants, currentUserId]);

    const handleSelectMention = useCallback(
        (item: { displayName: string }) => {
            if (!mentionToken) return;
            const insertion = `@${item.displayName.replace(/\s+/g, " ").trim()} `;
            const next = query.slice(0, mentionToken.start) + insertion + query.slice(mentionToken.end);
            const cursor = mentionToken.start + insertion.length;
            setQuery(next);
            previousLineCountRef.current = getExplicitLineCount(next);
            setPendingSelection({ start: cursor, end: cursor });
            // notifyTyping не дёргаем — это всё ещё ввод, useTypingIndicator
            // получит событие на следующем onChangeText.
        },
        [mentionToken, query],
    );

    useEffect(() => {
        if (editing) {
            setQuery(editing.body);
            previousLineCountRef.current = getExplicitLineCount(editing.body);
            setInputHeight(getExplicitLineContentHeight(editing.body));
        }
    }, [editing?.id]);

    // Когда query становится пустым — на вебе onContentSizeChange textarea не
    // схлопывается обратно к высоте одной строки. Принудительно сбрасываем
    // через requestAnimationFrame, чтобы перетереть устаревшее значение,
    // которое onContentSizeChange выставит после onChangeText.
    useEffect(() => {
        if (query.length !== 0) return;
        if (inputHeight === MIN_INPUT_CONTENT_HEIGHT) return;
        const raf = requestAnimationFrame(() => setInputHeight(MIN_INPUT_CONTENT_HEIGHT));
        return () => cancelAnimationFrame(raf);
    }, [inputHeight, query]);

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
                    useNativeDriver: Platform.OS !== "web",
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

    const handleQueryChange = useCallback(
        (text: string) => {
            const previousLineCount = previousLineCountRef.current;
            const nextLineCount = getExplicitLineCount(text);

            previousLineCountRef.current = nextLineCount;

            if (Platform.OS === "web" && nextLineCount !== previousLineCount) {
                const nextContentHeight = getExplicitLineContentHeight(text);
                pendingLineChangeContentHeightRef.current = nextContentHeight;
                pendingLineChangeDirectionRef.current =
                    nextLineCount > previousLineCount ? "grow" : "shrink";
                setInputHeight(nextContentHeight);
            }

            setQuery(text);
            if (!editing) notifyTyping(text);
        },
        [editing, notifyTyping],
    );

    const handleInputContentSizeChange = useCallback(
        (measuredHeight: number) => {
            const nextHeight = normalizeMeasuredContentHeight(measuredHeight, query);
            const pendingLineChangeHeight = pendingLineChangeContentHeightRef.current;
            const pendingLineChangeDirection = pendingLineChangeDirectionRef.current;

            if (
                pendingLineChangeHeight !== null &&
                pendingLineChangeDirection === "grow" &&
                nextHeight < pendingLineChangeHeight
            ) {
                pendingLineChangeContentHeightRef.current = null;
                pendingLineChangeDirectionRef.current = null;
                setInputHeight(pendingLineChangeHeight);
                return;
            }

            if (
                pendingLineChangeHeight !== null &&
                pendingLineChangeDirection === "shrink" &&
                nextHeight > pendingLineChangeHeight
            ) {
                pendingLineChangeContentHeightRef.current = null;
                pendingLineChangeDirectionRef.current = null;
                setInputHeight(pendingLineChangeHeight);
                return;
            }

            pendingLineChangeContentHeightRef.current = null;
            pendingLineChangeDirectionRef.current = null;
            setInputHeight(nextHeight);
        },
        [query],
    );

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
            previousLineCountRef.current = 1;
            pendingLineChangeContentHeightRef.current = null;
            pendingLineChangeDirectionRef.current = null;
            setInputHeight(MIN_INPUT_CONTENT_HEIGHT);
            return;
        }

        onSend(trimmed, attachments, replyTo?.id);

        setQuery("");
        clearAttachments();
        if (replyTo) setReply(null);
        previousLineCountRef.current = 1;
        pendingLineChangeContentHeightRef.current = null;
        pendingLineChangeDirectionRef.current = null;
        setInputHeight(MIN_INPUT_CONTENT_HEIGHT);
    };

    const handleInputKeyPress = (event: {
        nativeEvent?: {
            key?: string;
            shiftKey?: boolean;
            preventDefault?: () => void;
        };
        preventDefault?: () => void;
    }) => {
        if (Platform.OS !== "web") return;

        const nativeEvent = event.nativeEvent;
        if (nativeEvent?.key !== "Enter" || nativeEvent.shiftKey) return;

        nativeEvent.preventDefault?.();
        event.preventDefault?.();
        void handleSend();
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
            <MentionSuggestions items={mentionSuggestions} onSelect={handleSelectMention} />
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

                <View className="flex-1 flex-row items-center bg-white/5 rounded-2xl px-4">
                    <TextInput
                        ref={inputRef}
                        testID="chat-input-text"
                        className="text-white flex-1 text-[15px] outline-none focus:outline-none"
                        placeholder={editing ? "Изменение сообщения" : "Сообщение"}
                        placeholderTextColor="#8B8FA8"
                        value={query}
                        onChangeText={handleQueryChange}
                        onKeyPress={handleInputKeyPress}
                        selection={pendingSelection ?? undefined}
                        onSelectionChange={(e) => {
                            const next = e.nativeEvent.selection;
                            setSelection(next);
                            // Программная установка selection одноразовая — снимаем
                            // фиксацию после первого срабатывания, иначе RN не даст
                            // пользователю перемещать курсор.
                            if (pendingSelection) setPendingSelection(null);
                        }}
                        onFocus={() => isEmojiVisible && animate(false)}
                        multiline
                        onContentSizeChange={(e) =>
                            handleInputContentSizeChange(e.nativeEvent.contentSize.height)
                        }
                        style={{
                            height: getComposerInputHeight(inputHeight),
                            textAlignVertical: "center",
                            lineHeight: INPUT_LINE_HEIGHT,
                            paddingTop: INPUT_VERTICAL_PADDING,
                            paddingBottom: INPUT_VERTICAL_PADDING,
                            paddingLeft: 0,
                            paddingRight: 0,
                            outlineWidth: 0,
                        }}
                    />
                    <Pressable
                        testID="chat-input-emoji-button"
                        onPress={() => {
                            Keyboard.dismiss();
                            const willShow = !isEmojiVisible;
                            setIsEmojiVisible(willShow);
                            animate(willShow);
                        }}
                        className="py-2.5 ml-2"
                        style={{ alignSelf: "flex-end" }}
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
