import { AnimatedViewStyle } from "@/shared/types";
import { Logo } from "@/shared/ui";
import { Text, View } from "react-native";
import Animated from "react-native-reanimated";
interface HeaderProps {
    textAnimatedStyle: AnimatedViewStyle;
}
export const Header = ({ textAnimatedStyle }: HeaderProps) => {
    return (<View className="flex justify-start w-full py-[28px] px-6 flex-row gap-3 items-center">
      <Logo size={39}/>
      <Animated.View style={textAnimatedStyle}>
        <Text numberOfLines={1} className="text-white text-2xl font-extrabold tracking-tight">
          URFU LINK
        </Text>
      </Animated.View>
    </View>);
};
