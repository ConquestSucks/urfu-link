import { Avatar } from "@/shared/ui";
import { Text, View, Pressable, Linking } from "react-native";
import { ChatMessageProps } from "../model/types";
import {
    ArrowBendDoubleUpRightIcon,
    ChatsCircleIcon,
    ChecksIcon,
    ClockIcon,
    FileIcon,
    PencilSimpleIcon,
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

export const ChatMessage = ({
    text,
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
    onThreadOpen,
    onReactionPress,
}: ChatMessageProps) => {
    const currentUserId = useCurrentUserId();
    const isOptimistic = localStatus === "sending";
    const isFailed = localStatus === "failed";
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

    return (
        <View className={`flex-row gap-2 ${isOwn ? "justify-end" : "justify-start"}`}>
            {!isOwn && showAvatar && <Avatar size={40} src={avatarUrl} />}

            <Pressable
                onLongPress={onLongPress}
                delayLongPress={350}
                style={{ opacity: isOptimistic ? 0.6 : 1 }}
                className={`max-w-[85%] gap-1 px-3 py-3 rounded-2xl ${
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
                    <View
                        className={`pl-2 mb-1 border-l-2 ${
                            isOwn ? "border-white/60" : "border-brand-400"
                        }`}
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
                    </View>
                )}

                {attachments.length > 0 && (
                    <View className="mb-1 gap-2">
                        {attachments.map((file, index) => (
                            <Pressable
                                key={`${file.url}-${index}`}
                                onPress={() => Linking.openURL(file.url)}
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
                    {isOwn && !isOptimistic && !isFailed && (
                        <ChecksIcon
                            size={12}
                            className={seen ? "text-white" : "text-brand-300"}
                            weight="bold"
                        />
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
