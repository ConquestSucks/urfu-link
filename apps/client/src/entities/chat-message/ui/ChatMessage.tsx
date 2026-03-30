import { Avatar } from "@/shared/ui";
import { Text, View } from "react-native";
import { ChatMessageProps } from "../model/types";
export const ChatMessage = ({ text, isOwn, time, avatarUrl, showAvatar, }: ChatMessageProps) => {
    return (<View className={`px-4 py-3 relative ${isOwn ? "items-end" : "items-start"}`}>
      {!isOwn && showAvatar && (<View className="absolute left-4 z-10" style={{ top: 12, transform: [{ translateY: -20 }] }}>
          <Avatar size={40} src={avatarUrl}/>
        </View>)}

      <View className={`max-w-[85%] gap-1 px-5 py-3 ${isOwn
            ? "bg-brand-600 rounded-2xl rounded-tr-none"
            : `bg-white/5 border border-white/10 rounded-2xl rounded-tl-none ${showAvatar ? "ml-12" : ""}`}`}>
        <Text className="text-[15px] leading-[22px] text-white">{text}</Text>

        <Text className={`text-[10px] font-medium mt-1 ${isOwn ? "text-white/70" : "text-text-placeholder"} text-right`}>
          {time}
        </Text>
      </View>
    </View>);
};
