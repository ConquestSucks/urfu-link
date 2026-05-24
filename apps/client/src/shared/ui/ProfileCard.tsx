import { AnimatedView } from "@/shared/lib/nativewind-interop";
import { Text, View } from "react-native";
import { Avatar } from "./Avatar";
import { Skeleton } from "./Skeleton";
export interface ProfileProps {
    textAnimatedStyle?: any;
    userName: string;
    userDescription: string;
    avatarUrl?: string;
    avatarSize: number;
    isLoading?: boolean;
}
export const ProfileCard = ({ textAnimatedStyle, userName, userDescription, avatarUrl, avatarSize, isLoading = false, }: ProfileProps) => {
    if (isLoading) {
        return (<View className="flex-row gap-3 items-center">
      <Skeleton
        testID="profile-card-avatar-skeleton"
        style={{ width: avatarSize, height: avatarSize }}
        className="rounded-xl shrink-0"
      />
      <AnimatedView style={textAnimatedStyle ?? {}}>
        <View className="gap-1.5">
          <Skeleton testID="profile-card-name-skeleton" className="h-3.5 w-28 rounded" />
          <Skeleton testID="profile-card-description-skeleton" className="h-2.5 w-20 rounded bg-white/5" />
        </View>
      </AnimatedView>
    </View>);
    }

    return (<View className="flex-row gap-3 items-center">
      <Avatar src={avatarUrl} size={avatarSize} name={userName}/>
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
