import { useCurrentUser } from "@/entities/user";
import { AnimatedView } from "@/shared/lib/nativewind-interop";
import { ProfileCard } from "@/shared/ui";
import { CaretLeftIcon, GearIcon } from "@/shared/ui/phosphor";
import { Pressable, Text, View } from "react-native";

export interface ProfileProps {
  textAnimatedStyle: any;
  chevronAnimatedStyle: any;
  handleToggle: () => void;
  onSettingsPress: () => void;
}

export const Profile = ({
  textAnimatedStyle,
  chevronAnimatedStyle,
  handleToggle,
  onSettingsPress,
}: ProfileProps) => {
  const { data: profile } = useCurrentUser();

  const userName = profile?.identity.name ?? "";
  const userDescription = profile?.account.aboutMe ?? "";
  const avatarUrl = profile?.account.avatarUrl ?? undefined;

  return (
    <View className="p-4 border-t border-white/5 gap-3">
      <Pressable
        className="flex-row justify-between items-center bg-app-card rounded-2xl p-[7.5px] transition-colors active:bg-white/10 hover:bg-white/5"
        onPress={onSettingsPress}
      >
        {({ pressed, hovered }) => (
          <>
            <ProfileCard
              textAnimatedStyle={textAnimatedStyle}
              userName={userName}
              userDescription={userDescription}
              avatarUrl={avatarUrl}
              avatarSize={40}
            />

            <AnimatedView style={textAnimatedStyle}>
              <View className="p-[6px] rounded-xl">
                <GearIcon
                  size={16}
                  className={pressed ? "text-brand-600" : hovered ? "text-brand-400" : "text-text-disabled"}
                />
              </View>
            </AnimatedView>
          </>
        )}
      </Pressable>

      <Pressable
        className={`text-text-placeholder px-[18.5] py-[11px] flex-row items-center gap-2 rounded-xl`}
        onPress={handleToggle}
      >
        <AnimatedView style={chevronAnimatedStyle}>
          <CaretLeftIcon className="text-text-placeholder" size={18} />
        </AnimatedView>
        <AnimatedView style={textAnimatedStyle}>
          <Text numberOfLines={1} className="text-text-placeholder text-sm font-medium">
            Свернуть
          </Text>
        </AnimatedView>
      </Pressable>
    </View>
  );
};
