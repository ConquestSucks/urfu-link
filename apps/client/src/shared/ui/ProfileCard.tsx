import { AnimatedView } from "@/shared/lib/nativewind-interop";
import { Text, View } from "react-native";
import { Avatar } from "./Avatar";
export interface ProfileProps {
    textAnimatedStyle?: any;
    userName: string;
    userDescription: string;
    avatarUrl?: string;
    avatarSize: number;
}
export const ProfileCard = ({ textAnimatedStyle, userName, userDescription, avatarUrl, avatarSize, }: ProfileProps) => {
    return (<View className="flex-row gap-3 items-center">
      <Avatar src={avatarUrl} size={avatarSize}/>
      <AnimatedView style={textAnimatedStyle ?? {}}>
        <View className="gap-1.5">
          <Text numberOfLines={1} className="text-white text-sm leading-none">
            {userName}
          </Text>
          <Text numberOfLines={1} className="text-text-muted text-[11px] leading-none">
            {userDescription}
          </Text>
        </View>
      </AnimatedView>
    </View>);
};
