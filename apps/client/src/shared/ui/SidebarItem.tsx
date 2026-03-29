import React from "react";
import { Pressable, Text, View } from "react-native";
import Animated from "react-native-reanimated";
import { AnimatedViewStyle } from "../types";
interface SidebarItemProps {
    icon: React.ComponentType<any>;
    label: string;
    isActive: boolean;
    textAnimatedStyle?: AnimatedViewStyle;
    onPress?: () => void;
}
export const SidebarItem = ({ icon: Icon, label, isActive, textAnimatedStyle, onPress, }: SidebarItemProps) => {
    return (<Pressable className={`flex-row gap-3 items-center px-[17.5] py-[14px] rounded-xl transition-colors duration-300 select-none ${isActive
            ? "bg-[#2B7FFF] shadow-[0_10px_15px_-3px_rgba(43,127,255,0.2),0_4px_6px_-3px_rgba(43,127,255,0.2)]"
            : "bg-transparent hover:bg-white/5"}`} onPress={onPress}>
      <View>
        <Icon size={20} color={isActive ? "#FFFFFF" : "#90A1B9"}/>
      </View>
      <Animated.View style={textAnimatedStyle ?? {}}>
        <Text numberOfLines={1} className={`text-[15px] font-medium ${isActive ? "text-white" : "text-[#90A1B9]"}`}>
          {label}
        </Text>
      </Animated.View>
    </Pressable>);
};
