import { View } from "react-native";
import Animated from "react-native-reanimated";
import { useSidebarAnimation } from "../model/useSidebarAnimation";
import { Header } from "./Header";
import { Menu } from "./Menu";
import { Profile } from "./Profile";
export interface GlobalSidebarProps {
    onSettingsPress: () => void;
}
export const GlobalSidebar = ({ onSettingsPress }: GlobalSidebarProps) => {
    const { handleToggle, sidebarStyle, textAnimatedStyle, chevronAnimatedStyle, } = useSidebarAnimation();
    return (<Animated.View style={sidebarStyle} className="h-full bg-app-bg">
      <Header textAnimatedStyle={textAnimatedStyle}/>

      <View className="w-full grow px-4 gap-y-2">
        <Menu textAnimatedStyle={textAnimatedStyle}/>
      </View>

      <Profile textAnimatedStyle={textAnimatedStyle} chevronAnimatedStyle={chevronAnimatedStyle} handleToggle={handleToggle} onSettingsPress={onSettingsPress}/>
    </Animated.View>);
};
