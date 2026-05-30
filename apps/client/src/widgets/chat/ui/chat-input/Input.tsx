import React, {
    forwardRef,
    useCallback,
    useEffect,
    useImperativeHandle,
    useMemo,
    useRef,
    useState,
} from "react";
import { View, Pressable, Text, TextInput, Keyboard, Animated, Dimensions, Platform } from "react-native";
import {
    PaperPlaneRightIcon,
    MicrophoneIcon,
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
import type { ConversationParticipantDto } from "@urfu-link/api-client";
import { ActivityIndicator } from "@/shared/ui/activity-indicator";
import {
    useVoiceRecorder,
    VoiceDraftPreview,
    VoiceRecorderBar,
    type VoiceRecordingDraft,
} from "@/features/voice-message";

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
    onSend: (
        text: string,
        files: DocumentPickerAsset[],
        replyToMessageId?: string,
        mentionUserIds?: string[],
        voiceDraft?: VoiceRecordingDraft | null,
    ) => void | Promise<void>;
    typingEnabled?: boolean;
    composerMode?: "conversation" | "thread";
}

export type ChatInputHandle = {
    addFilesAndOpenModal: (files: File[]) => void;
};

type SelectedMention = {
    userId: string;
    label: string;
};

const normalizeMentionLabel = (displayName: string | null | undefined) =>
    displayName?.replace(/\s+/g, " ").trim() || "Пользователь";

const mentionTextFor = (label: string) => `@${label}`;

export const ChatInput = forwardRef<ChatInputHandle, ChatInputProps>(
    ({ conversationId, onSend, typingEnabled = true, composerMode = "conversation" }, ref) => {
    const [query, setQuery] = useState("");
    const { onTextChange: notifyTyping, onSend: notifyStopTyping } = useTypingIndicator(
        conversationId,
        { enabled: typingEnabled },
    );
    const [isEmojiVisible, setIsEmojiVisible] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [submitError, setSubmitError] = useState<string | null>(null);
    const [inputHeight, setInputHeight] = useState(MIN_INPUT_CONTENT_HEIGHT);
    const [selectedMentions, setSelectedMentions] = useState<SelectedMention[]>([]);
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

    const storeReplyTo = useComposerStore((s) => s.replyTo);
    const storeEditing = useComposerStore((s) => s.editing);
    const resetComposer = useComposerStore((s) => s.reset);
    const setReply = useComposerStore((s) => s.setReply);
    const editMessage = useChatStore((s) => s.editMessage);
    const currentUserId = useCurrentUserId();
    const participants = useConversationParticipants(conversationId);
    const replyTo = composerMode === "conversation" ? storeReplyTo : null;
    const editing = composerMode === "conversation" ? storeEditing : null;

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
        (item: ConversationParticipantDto) => {
            if (!mentionToken) return;
            const label = normalizeMentionLabel(item.displayName);
            const insertion = `${mentionTextFor(label)} `;
            const next = query.slice(0, mentionToken.start) + insertion + query.slice(mentionToken.end);
            const cursor = mentionToken.start + insertion.length;
            setQuery(next);
            setSelectedMentions((prev) =>
                prev.some((mention) => mention.userId === item.userId)
                    ? prev
                    : [...prev, { userId: item.userId, label }],
            );
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
            setSelectedMentions([]);
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
        addAttachments,
        handleAttachFiles,
        removeAttachment,
        clearAttachments,
    } = useAttachments(MAX_FILES_LIMIT, () => animate(false));
    const voiceRecorder = useVoiceRecorder();
    const discardVoiceDraft = voiceRecorder.discardDraft;
    const startVoiceRecording = voiceRecorder.startRecording;

    const hasVoiceDraft = !!voiceRecorder.draft;
    const canRecordVoice =
        !isSubmitting &&
        !editing &&
        query.trim().length === 0 &&
        attachments.length === 0 &&
        !hasVoiceDraft &&
        !voiceRecorder.isRecording &&
        !voiceRecorder.isStarting;
    const canSend = !isSubmitting && (query.trim().length > 0 || attachments.length > 0 || hasVoiceDraft);

    const resetInput = useCallback(() => {
        setQuery("");
        setSelectedMentions([]);
        clearAttachments();
        discardVoiceDraft();
        if (replyTo) setReply(null);
        previousLineCountRef.current = 1;
        pendingLineChangeContentHeightRef.current = null;
        pendingLineChangeDirectionRef.current = null;
        setInputHeight(MIN_INPUT_CONTENT_HEIGHT);
    }, [clearAttachments, discardVoiceDraft, replyTo, setReply]);

    useImperativeHandle(
        ref,
        () => ({
            addFilesAndOpenModal: (files: File[]) => {
                if (files.length === 0 || editing || hasVoiceDraft || voiceRecorder.isRecording) return;
                setSubmitError(null);
                animate(false);
                addAttachments(files, { openModal: true });
            },
        }),
        [addAttachments, animate, editing, hasVoiceDraft, voiceRecorder.isRecording],
    );

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
            if (submitError) setSubmitError(null);
            setSelectedMentions((prev) =>
                prev.filter((mention) => text.includes(mentionTextFor(mention.label))),
            );
            if (!editing) notifyTyping(text);
        },
        [editing, notifyTyping, submitError],
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

    const submitComposer = useCallback(async () => {
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
            setSelectedMentions([]);
            resetComposer();
            previousLineCountRef.current = 1;
            pendingLineChangeContentHeightRef.current = null;
            pendingLineChangeDirectionRef.current = null;
            setInputHeight(MIN_INPUT_CONTENT_HEIGHT);
            return;
        }

        const mentionUserIds = selectedMentions
            .filter((mention) => trimmed.includes(mentionTextFor(mention.label)))
            .map((mention) => mention.userId);
        const voiceDraft = voiceRecorder.draft;

        setSubmitError(null);
        setIsSubmitting(true);
        try {
            if (voiceDraft) {
                await onSend("", [], undefined, undefined, voiceDraft);
            } else if (mentionUserIds.length > 0) {
                await onSend(trimmed, attachments, replyTo?.id, mentionUserIds);
            } else {
                await onSend(trimmed, attachments, replyTo?.id);
            }
            resetInput();
        } catch (error) {
            console.error("Failed to submit composer", error);
            setSubmitError("Не удалось отправить. Попробуйте ещё раз.");
        } finally {
            setIsSubmitting(false);
        }
    }, [
        attachments,
        canSend,
        editMessage,
        editing,
        notifyStopTyping,
        onSend,
        query,
        replyTo?.id,
        resetComposer,
        resetInput,
        selectedMentions,
        voiceRecorder.draft,
    ]);

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
        void submitComposer();
    };

    const handleStartVoiceRecording = useCallback(() => {
        if (!canRecordVoice) return;
        Keyboard.dismiss();
        animate(false);
        notifyStopTyping();
        setSubmitError(null);
        void startVoiceRecording();
    }, [animate, canRecordVoice, notifyStopTyping, startVoiceRecording]);

    const composerHint = editing
        ? { icon: "edit" as const, title: "Изменение", preview: editing.body }
        : replyTo
            ? { icon: "reply" as const, title: "Ответ", preview: replyTo.body }
            : null;

    const cancelComposerHint = () => {
        if (editing) {
            resetComposer();
            setQuery("");
            setSubmitError(null);
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
                onRemove={(index) => {
                    setSubmitError(null);
                    removeAttachment(index);
                }}
                onOpenModal={() => setIsFilesModalVisible(true)}
                disabled={isSubmitting}
            />

            {submitError ? (
                <Text className="text-danger-300 text-xs font-medium mb-2 pl-1">
                    {submitError}
                </Text>
            ) : null}

            {voiceRecorder.error ? (
                <Text className="text-danger-300 text-xs font-medium mb-2 pl-1">
                    {voiceRecorder.error}
                </Text>
            ) : null}

            {voiceRecorder.isRecording ? (
                <VoiceRecorderBar
                    durationSeconds={voiceRecorder.durationSeconds}
                    maxDurationSeconds={voiceRecorder.maxDurationSeconds}
                    isStopping={voiceRecorder.isStopping}
                    onStop={() => {
                        void voiceRecorder.stopRecording();
                    }}
                    onCancel={voiceRecorder.cancelRecording}
                />
            ) : voiceRecorder.draft ? (
                <VoiceDraftPreview
                    draft={voiceRecorder.draft}
                    isSubmitting={isSubmitting}
                    onDelete={voiceRecorder.discardDraft}
                    onSubmit={submitComposer}
                />
            ) : (
            <View className="flex-row items-end gap-3">
                <Pressable
                    onPress={() => {
                        animate(false);
                        setIsFilesModalVisible(true);
                    }}
                    className={`mb-2 ${attachments.length >= MAX_FILES_LIMIT || !!editing || isSubmitting || voiceRecorder.isStarting ? "opacity-30" : "active:opacity-60"}`}
                    disabled={attachments.length >= MAX_FILES_LIMIT || !!editing || isSubmitting || voiceRecorder.isStarting}
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
                        editable={!isSubmitting}
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
                        disabled={isSubmitting}
                        onPress={() => {
                            Keyboard.dismiss();
                            const willShow = !isEmojiVisible;
                            setIsEmojiVisible(willShow);
                            animate(willShow);
                        }}
                        className={`py-2.5 ml-2 ${isSubmitting ? "opacity-40" : ""}`}
                        style={{ alignSelf: "flex-end" }}
                    >
                        <SmileyIcon
                            size={24}
                            className={isEmojiVisible ? "text-brand-600" : "text-text-subtle"}
                            weight={isEmojiVisible ? "fill" : "regular"}
                        />
                    </Pressable>
                </View>

                {query.trim().length === 0 && attachments.length === 0 && !editing ? (
                    <Pressable
                        testID="chat-input-voice-button"
                        onPress={handleStartVoiceRecording}
                        disabled={!canRecordVoice}
                        className={`w-11 h-11 rounded-full items-center justify-center ${
                            canRecordVoice ? "bg-brand-600 active:opacity-80" : "bg-brand-600/30"
                        }`}
                    >
                        {voiceRecorder.isStarting ? (
                            <ActivityIndicator testID="chat-input-voice-spinner" size="small" className="text-white" />
                        ) : (
                            <MicrophoneIcon
                                size={22}
                                className={canRecordVoice ? "text-white" : "text-white/40"}
                                weight="fill"
                            />
                        )}
                    </Pressable>
                ) : (
                    <Pressable
                        testID="chat-input-send-button"
                        onPress={submitComposer}
                        disabled={!canSend}
                        className={`w-11 h-11 rounded-full items-center justify-center ${isSubmitting ? "bg-brand-600/70" : canSend ? "bg-brand-600 active:opacity-80" : "bg-brand-600/30"}`}
                    >
                        {isSubmitting ? (
                            <ActivityIndicator testID="chat-input-send-spinner" size="small" className="text-white" />
                        ) : (
                            <PaperPlaneRightIcon
                                size={22}
                                className={canSend ? "text-white" : "text-white/40"}
                                weight="fill"
                            />
                        )}
                    </Pressable>
                )}
            </View>
            )}

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
                onPickFiles={() => {
                    setSubmitError(null);
                    void handleAttachFiles();
                }}
                onAddDroppedFiles={(files) => {
                    setSubmitError(null);
                    addAttachments(files, { openModal: true });
                }}
                onRemove={(index) => {
                    setSubmitError(null);
                    removeAttachment(index);
                }}
                onSubmit={submitComposer}
                isSubmitting={isSubmitting}
                submitError={submitError}
            />
        </View>
    );
});

ChatInput.displayName = "ChatInput";
