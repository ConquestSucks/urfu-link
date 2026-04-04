import { Avatar } from "@/shared/ui";
import { Text, View, Pressable, Linking } from "react-native";
import { ChatMessageProps } from "../model/types";
import { ChecksIcon, FileIcon } from "@/shared/ui/phosphor";

export const ChatMessage = ({
    text,
    isOwn,
    time,
    avatarUrl,
    showAvatar,
    seen,
    attachments = [],
}: ChatMessageProps) => {
    return (
        <View className={`flex-row gap-2 ${isOwn ? "justify-end" : "justify-start"}`}>
            {!isOwn && showAvatar && <Avatar size={40} src={avatarUrl} />}

            <View
                className={`max-w-[85%] gap-1 px-3 py-3 rounded-2xl ${
                    isOwn ? "bg-brand-600" : `bg-white/5`
                }`}
            >
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
                        {text}
                    </Text>
                )}

                <View className="flex-row items-center gap-1 justify-end">
                    <Text
                        className={`text-[10px] font-medium ${
                            isOwn ? "text-white/70" : "text-text-placeholder"
                        } text-right`}
                    >
                        {time}
                    </Text>
                    {isOwn && (
                        <ChecksIcon
                            size={12}
                            className={seen ? "text-white" : "text-brand-300"}
                            weight="bold"
                        />
                    )}
                </View>
            </View>
        </View>
    );
};