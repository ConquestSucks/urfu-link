import { View } from "react-native";

interface ChatMessageSkeletonProps {
    isOwn?: boolean;
    showAvatar?: boolean;
}

export const ChatMessageSkeleton = ({
    isOwn = false,
    showAvatar = false,
}: ChatMessageSkeletonProps) => {
    return (
        <View className={`flex-row gap-2 ${isOwn ? "justify-end" : "justify-start"}`}>
            {!isOwn && showAvatar && (
                <View className="animate-pulse w-10 h-10 rounded-full bg-white/10" />
            )}

            <View
                className={`max-w-[85%] gap-2 px-3 py-3 rounded-2xl animate-pulse ${
                    isOwn ? "bg-brand-600/50" : `bg-white/5`
                }`}
            >
                <View className="w-48 h-[15px] bg-white/20 rounded mt-1" />
                <View className="w-32 h-[15px] bg-white/20 rounded" />

                <View className="flex-row items-center gap-1 justify-end mt-1">
                    <View className="w-10 h-2.5 bg-white/20 rounded" />
                </View>
            </View>
        </View>
    );
};
