import { Avatar } from "@/shared/ui";
import { Text, View, Pressable, Linking, Platform, type ViewStyle } from "react-native";
import { ChatMessageProps } from "../model/types";
import { apiClient } from "@/shared/lib/api";
import type { AttachmentType } from "@urfu-link/api-client";
import {
    ArrowBendDoubleUpRightIcon,
    ChatsCircleIcon,
    CheckIcon,
    ChecksIcon,
    ClockIcon,
    FileIcon,
    PencilSimpleIcon,
    PhoneIcon,
    VideoCameraIcon,
    TrashIcon,
    WarningCircleIcon,
} from "@/shared/ui/phosphor";
import { useCurrentUserId } from "@/shared/store/auth-store";
import { VoiceMessagePlayer } from "@/features/voice-message";
import { glow } from "@/shared/ui/glow";

const WEB_MESSAGE_BUBBLE_MAX_WIDTH = 680;
const IS_WEB = Platform.OS === "web";
const messageBubbleMaxWidthClass = IS_WEB ? "" : "max-w-[85%]";
const webMessageBubbleMaxWidthStyle = IS_WEB
    ? ({
          maxWidth: `min(85%, ${WEB_MESSAGE_BUBBLE_MAX_WIDTH}px)`,
      } as unknown as ViewStyle)
    : undefined;

const renderBodyWithMentions = (text: string) => {
    const parts = text.split(/(@[A-Za-zА-Яа-я0-9_.-]+)/g);
    return parts.map((part, i) =>
        part.startsWith("@") ? (
            <Text key={i} className="text-brand-300 font-semibold">
                {part}
            </Text>
        ) : (
            <Text key={i}>{part}</Text>
        ),
    );
};

type AttachmentFile = {
    name: string;
    url: string;
    mediaAssetId?: string;
    type?: AttachmentType;
    size?: number | null;
    mimeType?: string;
    durationSeconds?: number | null;
};

const startWebDownload = (url: string, fileName: string) => {
    if (typeof document === "undefined") return false;

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.target = "_blank";
    anchor.rel = "noopener noreferrer";
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    return true;
};

const openAttachmentUrl = async (url: string, fileName: string) => {
    if (Platform.OS === "web" && startWebDownload(url, fileName)) {
        return;
    }

    await Linking.openURL(url);
};

const parseDuration = (raw: string | null | undefined): number | null => {
    if (!raw) return null;

    const direct = /^\s*(\d+):(\d{2})(?::(\d{2}))?\s*$/;
    const directMatch = direct.exec(raw);
    if (directMatch) {
        const hours = Number(directMatch[3] ? directMatch[1] : 0);
        const minutes = Number(directMatch[3] ? directMatch[2] : directMatch[1]);
        const seconds = Number(directMatch[3] ?? 0);
        return hours * 3600 + minutes * 60 + seconds;
    }

    const iso = /^PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?$/i.exec(raw);
    if (!iso) return null;

    const hours = Number(iso[1] ?? 0);
    const minutes = Number(iso[2] ?? 0);
    const seconds = Number(iso[3] ?? 0);
    return hours * 3600 + minutes * 60 + Math.floor(seconds);
};

const formatDuration = (value: string | null | undefined): string | null => {
    const total = parseDuration(value);
    if (total == null) return null;

    const minutes = Math.floor(total / 60);
    const seconds = total % 60;
    if (minutes >= 60) {
        const hours = Math.floor(minutes / 60);
        const rest = minutes % 60;
        return `${hours}:${rest.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
    }

    return `${minutes}:${seconds.toString().padStart(2, "0")}`;
};

const formatAttachmentSize = (size?: number | null): string | null => {
    if (typeof size !== "number" || size <= 0) return null;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    if (size < 1024 * 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
    return `${(size / 1024 / 1024 / 1024).toFixed(1)} GB`;
};

const formatAttachmentDuration = (seconds?: number | null): string | null => {
    if (typeof seconds !== "number" || seconds <= 0) return null;
    const safeSeconds = Math.floor(seconds);
    const minutes = Math.floor(safeSeconds / 60);
    const rest = safeSeconds % 60;
    return `${minutes}:${rest.toString().padStart(2, "0")}`;
};

const buildAttachmentMeta = (file: AttachmentFile) =>
    [formatAttachmentDuration(file.durationSeconds), formatAttachmentSize(file.size)]
        .filter(Boolean)
        .join(", ");

const buildSystemCallLabel = (systemCall?: ChatMessageProps["systemCall"]) => {
    const callLabel = systemCall?.callType === "Video" ? "Видеозвонок" : "Звонок";

    switch (systemCall?.status) {
        case "Missed":
            return "Пропущенный звонок";
        case "Declined":
            return "Звонок отклонён";
        case "Cancelled":
            return "Звонок отменён";
        case "Failed":
            return "Звонок завершён";
        case "Completed": {
            const duration = formatDuration(systemCall.duration);
            return duration ? `Звонок завершён • ${duration}` : "Звонок завершён";
        }
        case "Started":
        default:
            return callLabel;
    }
};

const buildSystemCallTimeLabel = (
    status: NonNullable<ChatMessageProps["systemCall"]>["status"] | undefined,
    time: string,
) => {
    if (!time) return null;

    switch (status) {
        case "Started":
            return time;
        case "Missed":
            return `Пропущен: ${time}`;
        case "Declined":
            return `Отклонён: ${time}`;
        case "Cancelled":
            return `Отменён: ${time}`;
        case "Failed":
        case "Completed":
            return `Завершён: ${time}`;
        default:
            return time;
    }
};

export const ChatMessage = ({
    id,
    text,
    kind,
    systemCall,
    isOwn,
    time,
    avatarUrl,
    authorName,
    showAvatar,
    seen,
    attachments = [],
    replyTo,
    reactions,
    editedAtUtc,
    forwardedFrom,
    isDeleted,
    threadReplyCount = 0,
    localStatus,
    onLongPress,
    onContextMenu,
    onAuthorPress,
    onReplyPress,
    onThreadOpen,
    onReactionPress,
    showJoinCallAction,
    onJoinCall,
}: ChatMessageProps) => {
    const currentUserId = useCurrentUserId();
    const isOptimistic = localStatus === "sending";
    const isFailed = localStatus === "failed";
    const renderAuthorAvatar = () => {
        if (isOwn || !showAvatar) return null;

        const avatar = <Avatar size={40} src={avatarUrl} name={authorName} />;
        if (!onAuthorPress) return avatar;

        return (
            <Pressable
                testID={`chat-message-avatar-${id}`}
                onPress={onAuthorPress}
                hitSlop={4}
                accessibilityRole="button"
                className="shrink-0 cursor-pointer active:opacity-70"
            >
                {avatar}
            </Pressable>
        );
    };

    if (kind === "SystemCall") {
        const systemText = buildSystemCallLabel(systemCall);
        const timeLabel = buildSystemCallTimeLabel(systemCall?.status, time);
        const duration = formatDuration(systemCall?.duration);
        const canJoinCall =
            showJoinCallAction &&
            systemCall?.status === "Started" &&
            !!systemCall.callId &&
            !!onJoinCall;
        return (
            <View className="px-2 items-center">
                <View className="px-3 py-2 rounded-2xl bg-white/5 border border-white/10" style={canJoinCall ? glow.brandSubtle : glow.surface}>
                    <View className="flex-row items-center gap-2">
                        {systemCall?.callType === "Video" ? (
                            <VideoCameraIcon size={12} className="text-text-muted" />
                        ) : (
                            <PhoneIcon size={12} className="text-text-muted" />
                        )}
                        <Text className="text-[13px] text-text-subtle font-medium">
                            {systemText}
                        </Text>
                    </View>
                    <View className="mt-1 flex-row flex-wrap gap-2 justify-center">
                        {timeLabel ? (
                            <Text className="text-[11px] text-text-placeholder">
                                {timeLabel}
                            </Text>
                        ) : null}
                        {duration ? (
                            <Text className="text-[11px] text-text-placeholder">
                                Длительность: {duration}
                            </Text>
                        ) : null}
                    </View>
                    {canJoinCall ? (
                        <Pressable
                            testID={`system-call-join-${id}`}
                            onPress={() => onJoinCall(systemCall.callId)}
                            className="mt-2 self-center rounded-full bg-brand-600 px-3 py-1.5 active:opacity-80"
                            style={glow.brandSoft}
                        >
                            <Text className="text-[12px] font-semibold text-white">Войти</Text>
                        </Pressable>
                    ) : null}
                </View>
            </View>
        );
    }

    const openAttachment = async (file: AttachmentFile) => {
        try {
            if (file.mediaAssetId) {
                const { downloadUrl } = await apiClient.media.getAssetDownloadUrl(file.mediaAssetId);
                await openAttachmentUrl(downloadUrl, file.name);
                return;
            }

            await openAttachmentUrl(file.url, file.name);
        } catch (error) {
            console.error("Failed to open attachment", error);
        }
    };

    if (isDeleted) {
        return (
            <View className={`flex-row gap-2 ${isOwn ? "justify-end" : "justify-start"}`}>
                {renderAuthorAvatar()}
                <View
                    className={`${messageBubbleMaxWidthClass} px-3 py-2 rounded-2xl bg-white/5 flex-row items-center gap-2`}
                    style={[webMessageBubbleMaxWidthStyle, glow.surface]}
                >
                    <TrashIcon size={14} className="text-text-muted" />
                    <Text className="text-text-muted text-[13px] italic">Сообщение удалено</Text>
                </View>
            </View>
        );
    }

    const reactionEntries = reactions ? Object.entries(reactions).filter(([, ids]) => ids.length > 0) : [];
    const contextMenuProps =
        Platform.OS === "web" && onContextMenu
            ? {
                  onContextMenu: (event: any) => {
                      event.preventDefault?.();
                      event.stopPropagation?.();
                      const nativeEvent = event.nativeEvent ?? event;
                      onContextMenu({
                          x: nativeEvent.pageX ?? nativeEvent.clientX ?? 0,
                          y: nativeEvent.pageY ?? nativeEvent.clientY ?? 0,
                      });
                  },
              }
            : {};
    const hasAttachments = attachments.length > 0;
    const hasOnlyMediaContent = hasAttachments && !text;

    return (
        <View className={`flex-row gap-2 ${isOwn ? "justify-end" : "justify-start"}`}>
            {renderAuthorAvatar()}

            <Pressable
                onLongPress={onLongPress}
                delayLongPress={350}
                {...contextMenuProps}
                testID={`chat-message-bubble-${id}`}
                className={`${messageBubbleMaxWidthClass} cursor-default ${hasAttachments ? "min-w-[240px]" : ""} gap-1 px-3 ${
                    hasOnlyMediaContent ? "py-2.5" : "py-3"
                } rounded-2xl border border-transparent ${
                    isOwn ? "bg-brand-650" : "bg-white/5"
                }`}
                style={[
                    webMessageBubbleMaxWidthStyle,
                    { opacity: isOptimistic ? 0.6 : 1 },
                    isFailed ? glow.danger : isOwn ? glow.brandSoft : glow.surface,
                ]}
            >
                {forwardedFrom && (
                    <View className="flex-row items-center gap-1 mb-1 opacity-80">
                        <ArrowBendDoubleUpRightIcon size={12} className="text-white" />
                        <Text className="text-[11px] italic text-white">
                            Переслано
                        </Text>
                    </View>
                )}

                {!isOwn && authorName ? (
                    onAuthorPress ? (
                        <Pressable
                            testID={`chat-message-author-${id}`}
                            onPress={onAuthorPress}
                            hitSlop={4}
                            accessibilityRole="button"
                            className={`self-start cursor-pointer active:opacity-70 ${
                                hasAttachments ? "mb-1" : ""
                            }`}
                        >
                            <Text
                                className="text-[12px] font-semibold text-brand-300"
                                numberOfLines={1}
                            >
                                {authorName}
                            </Text>
                        </Pressable>
                    ) : (
                        <Text
                            className={`text-[12px] font-semibold text-brand-300 ${
                                hasAttachments ? "mb-1" : ""
                            }`}
                            numberOfLines={1}
                        >
                            {authorName}
                        </Text>
                    )
                ) : null}

                {replyTo && (
                    <Pressable
                        testID={`chat-message-reply-${id}`}
                        onPress={onReplyPress}
                        disabled={!onReplyPress}
                        hitSlop={4}
                        className={`pl-2 pr-2 py-1 -my-1 mb-1 rounded-r-lg border-l-2 transition-colors ${
                            isOwn ? "border-white/60" : "border-brand-400"
                        } ${onReplyPress ? "hover:bg-white/5 active:bg-white/10" : ""}`}
                    >
                        <Text
                            className="text-[12px] text-white font-semibold"
                            numberOfLines={1}
                        >
                            Ответ
                        </Text>
                        <Text
                            className="text-[12px] text-white"
                            numberOfLines={2}
                        >
                            {replyTo.preview}
                        </Text>
                    </Pressable>
                )}

                {hasAttachments && (
                    <View className={`${text ? "mb-1" : ""} gap-2`}>
                        {attachments.map((file, index) => {
                            const attachmentMeta = buildAttachmentMeta(file);
                            const itemKey = `${file.url}-${index}`;
                            const itemSeparator = index > 0 ? "pt-2 border-t border-white/10" : "";

                            return file.type === "Voice" ? (
                                <View key={itemKey} className={itemSeparator}>
                                    <VoiceMessagePlayer
                                        mediaAssetId={file.mediaAssetId}
                                        durationSeconds={file.durationSeconds}
                                        fileSizeBytes={file.size}
                                        isOwn={isOwn}
                                    />
                                </View>
                            ) : (
                                <Pressable
                                    key={itemKey}
                                    onPress={() => void openAttachment(file)}
                                    className={`flex-row items-center gap-2.5 active:opacity-60 ${itemSeparator}`}
                                    hitSlop={4}
                                >
                                    <View
                                        className={`w-10 h-10 rounded-full items-center justify-center ${
                                            isOwn ? "bg-white/20" : "bg-brand-650"
                                        }`}
                                        style={isOwn ? undefined : glow.brandSubtle}
                                    >
                                        <FileIcon size={20} className="text-white" weight="fill" />
                                    </View>
                                    <View className="flex-1 min-w-0">
                                        <Text
                                            className="text-[13px] font-semibold text-white"
                                            numberOfLines={1}
                                        >
                                            {file.name}
                                        </Text>
                                        {attachmentMeta ? (
                                            <Text
                                                className={`text-[11px] font-medium ${
                                                    isOwn ? "text-white/75" : "text-text-placeholder"
                                                }`}
                                                numberOfLines={1}
                                            >
                                                {attachmentMeta}
                                            </Text>
                                        ) : null}
                                    </View>
                                </Pressable>
                            );
                        })}
                    </View>
                )}

                {!!text && (
                    <Text className="text-[15px] leading-[22px] text-white">
                        {renderBodyWithMentions(text)}
                    </Text>
                )}

                {reactionEntries.length > 0 && (
                    <View className="flex-row flex-wrap gap-1 mt-1">
                        {reactionEntries.map(([emoji, userIds]) => {
                            const reactedByMe =
                                !!currentUserId && userIds.includes(currentUserId);
                            const Container: any = onReactionPress ? Pressable : View;
                            return (
                                <Container
                                    key={emoji}
                                    onPress={
                                        onReactionPress
                                            ? () => onReactionPress(emoji)
                                            : undefined
                                    }
                                    className={`flex-row items-center gap-1 px-2 py-0.5 rounded-full ${
                                        reactedByMe ? "bg-brand-500/30" : "bg-white/10"
                                    } ${onReactionPress ? "active:opacity-70" : ""}`}
                                    style={reactedByMe ? glow.brandSubtle : undefined}
                                >
                                    <Text className="text-[12px]">{emoji}</Text>
                                    <Text className="text-[11px] text-white">
                                        {userIds.length}
                                    </Text>
                                </Container>
                            );
                        })}
                    </View>
                )}

                {threadReplyCount > 0 && onThreadOpen && (
                    <Pressable
                        onPress={onThreadOpen}
                        className="flex-row items-center gap-1 mt-1 active:opacity-70"
                        hitSlop={4}
                    >
                        <ChatsCircleIcon size={14} className="text-white" />
                        <Text className="text-[12px] text-white font-medium">
                            {threadReplyCount} {threadReplyCount === 1 ? "ответ" : "ответов"}
                        </Text>
                    </Pressable>
                )}

                <View className="flex-row items-center gap-1 justify-end">
                    {editedAtUtc && (
                        <View className="flex-row items-center mr-1">
                            <PencilSimpleIcon
                                size={10}
                                className={isOwn ? "text-white" : "text-text-placeholder"}
                            />
                            <Text
                                className={`text-[10px] font-medium ml-0.5 ${
                                    isOwn ? "text-white" : "text-text-placeholder"
                                }`}
                            >
                                изм.
                            </Text>
                        </View>
                    )}
                    <Text
                        className={`text-[10px] font-medium ${
                            isOwn ? "text-white" : "text-text-placeholder"
                        } text-right`}
                    >
                        {time}
                    </Text>
                    {isOwn && !isOptimistic && !isFailed && seen && (
                        <ChecksIcon size={12} className="text-white" weight="bold" />
                    )}
                    {isOwn && !isOptimistic && !isFailed && !seen && (
                        <CheckIcon size={12} className="text-white" weight="bold" />
                    )}
                    {isOwn && isOptimistic && (
                        <ClockIcon size={12} className="text-white" />
                    )}
                    {isOwn && isFailed && (
                        <WarningCircleIcon
                            size={12}
                            className="text-danger-400"
                            weight="bold"
                        />
                    )}
                </View>
            </Pressable>
        </View>
    );
};

