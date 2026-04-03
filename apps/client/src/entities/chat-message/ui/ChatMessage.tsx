import { Avatar } from "@/shared/ui";
import { Text, View } from "react-native";
import { ChatMessageProps } from "../model/types";
import { ChecksIcon } from "@/shared/ui/phosphor";

export const ChatMessage = ({
    text,
    isOwn,
    time,
    avatarUrl,
    showAvatar,
    seen,
}: ChatMessageProps) => {
    return (
        <View className={`flex-row gap-2 ${isOwn ? "justify-end" : "justify-start"}`}>
            {!isOwn && showAvatar && <Avatar size={40} src={avatarUrl} />}

            <View
                className={`max-w-[85%] gap-1 px-3 py-3 rounded-2xl rounded ${
                    isOwn ? "bg-brand-600" : `bg-white/5`
                }`}
            >
                <Text className="text-[15px] leading-[22px] text-white">{text}</Text>

                <View className="flex-row items-center gap-1 justify-end">
                    <Text
                        className={`text-[10px] font-medium ${isOwn ? "text-white/70" : "text-text-placeholder"} text-right`}
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
