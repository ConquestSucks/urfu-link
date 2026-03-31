import { AnimatedView } from "@/shared/lib/nativewind-interop";
import { View } from "react-native";
import { useSidebarAnimation } from "../model/useSidebarAnimation";
import { Header } from "./Header";
import { Menu } from "./Menu";
import { Profile } from "./Profile";
export interface SidebarDesktopProps {
    onSettingsPress: () => void;
}
export const SidebarDesktop = ({ onSettingsPress }: SidebarDesktopProps) => {
    const { handleToggle, sidebarStyle, textAnimatedStyle, chevronAnimatedStyle, } = useSidebarAnimation();
    return (<AnimatedView style={sidebarStyle}>
      <View className="h-full bg-app-bg">
        <Header textAnimatedStyle={textAnimatedStyle}/>

        <View className="w-full grow px-4 gap-y-2">
          <Menu textAnimatedStyle={textAnimatedStyle}/>
        </View>

        <Profile textAnimatedStyle={textAnimatedStyle} chevronAnimatedStyle={chevronAnimatedStyle} handleToggle={handleToggle} onSettingsPress={onSettingsPress}/>
      </View>
    </AnimatedView>);
};
