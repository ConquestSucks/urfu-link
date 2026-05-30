import { Avatar } from "@/shared/ui";
import { Text, View, Pressable, Linking, Platform } from "react-native";
import { ChatMessageProps } from "../model/types";
import { apiClient } from "@/shared/lib/api";
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
        return `${hours.toString().padStart(2, "0")}:${rest.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
    }

    return `${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
};

const buildSystemCallLabel = (systemCall?: ChatMessageProps["systemCall"]) => {
    const callLabel = systemCall?.callType === "Video" ? "Видеозвонок" : "Звонок";

    switch (systemCall?.status) {
        case "Missed":
        case "Declined":
        case "Cancelled":
        case "Failed":
            return "Пропущенный звонок";
        case "Completed": {
            const duration = formatDuration(systemCall.duration);
            return duration ? `${callLabel} завершён • ${duration}` : `${callLabel} завершён`;
        }
        case "Started":
        default:
            return callLabel;
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
    onReplyPress,
    onThreadOpen,
    onReactionPress,
}: ChatMessageProps) => {
    const currentUserId = useCurrentUserId();
    const isOptimistic = localStatus === "sending";
    const isFailed = localStatus === "failed";

    if (kind === "SystemCall") {
        const systemText = buildSystemCallLabel(systemCall);
        return (
            <View className="px-2 items-center">
                <View className="px-3 py-2 rounded-full bg-white/5 border border-white/10">
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
                {!isOwn && showAvatar && <Avatar size={40} src={avatarUrl} />}
                <View className="max-w-[85%] px-3 py-2 rounded-2xl bg-white/5 flex-row items-center gap-2">
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

    return (
        <View className={`flex-row gap-2 ${isOwn ? "justify-end" : "justify-start"}`}>
            {!isOwn && showAvatar && <Avatar size={40} src={avatarUrl} />}

            <Pressable
                onLongPress={onLongPress}
                delayLongPress={350}
                {...contextMenuProps}
                style={{ opacity: isOptimistic ? 0.6 : 1 }}
                testID={`chat-message-bubble-${id}`}
                className={`max-w-[85%] gap-1 px-3 py-3 rounded-2xl border border-transparent ${
                    isOwn ? "bg-brand-600" : "bg-white/5"
                }`}
            >
                {forwardedFrom && (
                    <View className="flex-row items-center gap-1 mb-1 opacity-80">
                        <ArrowBendDoubleUpRightIcon size={12} className="text-white" />
                        <Text className="text-[11px] italic text-white/80">
                            Переслано
                        </Text>
                    </View>
                )}

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
                            className="text-[12px] text-white/80 font-semibold"
                            numberOfLines={1}
                        >
                            Ответ
                        </Text>
                        <Text
                            className="text-[12px] text-white/70"
                            numberOfLines={2}
                        >
                            {replyTo.preview}
                        </Text>
                    </Pressable>
                )}

                {attachments.length > 0 && (
                    <View className="mb-1 gap-2">
                        {attachments.map((file, index) => (
                            <Pressable
                                key={`${file.url}-${index}`}
                                onPress={() => void openAttachment(file)}
                                className={`flex-row items-center px-3 py-2.5 rounded-xl gap-2 active:opacity-60 ${
                                    isOwn ? "bg-white/20" : "bg-white/10"
                                }`}
                            >
                                <FileIcon size={20} className="text-white" />
                                <Text
                                    className="text-[13px] font-medium text-white flex-1"
                                    numberOfLines={1}
                                >
                                    {file.name}
                                </Text>
                            </Pressable>
                        ))}
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
                                >
                                    <Text className="text-[12px]">{emoji}</Text>
                                    <Text className="text-[11px] text-white/80">
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
                        <ChatsCircleIcon size={14} className="text-white/80" />
                        <Text className="text-[12px] text-white/80 font-medium">
                            {threadReplyCount} {threadReplyCount === 1 ? "ответ" : "ответов"}
                        </Text>
                    </Pressable>
                )}

                <View className="flex-row items-center gap-1 justify-end">
                    {editedAtUtc && (
                        <View className="flex-row items-center mr-1">
                            <PencilSimpleIcon
                                size={10}
                                className={isOwn ? "text-white/70" : "text-text-placeholder"}
                            />
                            <Text
                                className={`text-[10px] font-medium ml-0.5 ${
                                    isOwn ? "text-white/70" : "text-text-placeholder"
                                }`}
                            >
                                изм.
                            </Text>
                        </View>
                    )}
                    <Text
                        className={`text-[10px] font-medium ${
                            isOwn ? "text-white/70" : "text-text-placeholder"
                        } text-right`}
                    >
                        {time}
                    </Text>
                    {isOwn && !isOptimistic && !isFailed && seen && (
                        <ChecksIcon size={12} className="text-white" weight="bold" />
                    )}
                    {isOwn && !isOptimistic && !isFailed && !seen && (
                        <CheckIcon size={12} className="text-white/70" weight="bold" />
                    )}
                    {isOwn && isOptimistic && (
                        <ClockIcon size={12} className="text-white/70" />
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

